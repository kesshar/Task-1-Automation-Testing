using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ResilientFormTests;

/// <summary>
/// Resilient semantic control locator that discovers form elements without relying on
/// brittle positional indices, specific HTML tag hierarchies, or fragile IDs, names, or CSS classes.
///
/// Multi-Strategy Resolution:
/// 1. Primary Scoping: Locates the active form container using semantic heading heuristics and control clustering.
/// 2. Tier 1 (Explicit Accessibility): Matches standard HTML label associations (for/id), wrapping labels, and ARIA attributes.
/// 3. Tier 2 (Attribute Semantics): Matches placeholders, titles, and input values (for radio buttons).
/// 4. Tier 3 (Visual & DOM Proximity): Matches nearby text nodes, legend/span headers, sibling elements, and parent field containers.
/// </summary>
public sealed class SemanticControlFinder
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;

    // JavaScript to discover the most relevant form container.
    // Handles changed headings ("Form", "Practice Form", "Registration"), container types (<form>, <section>, <div>),
    // and falls back gracefully to document.body if the container structure was flattened.
    private const string FindPrimaryRootScript = """
        const norm = v => (v || '').replace(/\s+/g, ' ').trim().toLowerCase();
        const visible = el => {
            if (!el) return false;
            const style = getComputedStyle(el);
            return el.getClientRects().length > 0 && style.display !== 'none' && style.visibility !== 'hidden';
        };

        const hasTargetControls = el => {
            const texts = el.querySelectorAll("input[type='text'], input:not([type]), textarea, [role='textbox']");
            const radios = el.querySelectorAll("input[type='radio'], [role='radio']");
            const selects = el.querySelectorAll("select, [role='combobox']");
            return texts.length > 0 && radios.length > 0 && selects.length > 0;
        };

        // Strategy A: Headings indicating the primary form section (exact 'form' or form-related headings)
        const headings = Array.from(document.querySelectorAll('h1, h2, h3, h4, h5, h6, legend, [role="heading"]'))
            .filter(visible);

        // First prioritize heading titled exactly 'form' or 'automation practice form'
        const prioritized = headings.filter(h => {
            const text = norm(h.innerText);
            return text === 'form' || text === 'automation practice form' || text === 'practice form' || text.includes('form');
        });

        for (const heading of prioritized) {
            let node = heading;
            for (let depth = 0; node && depth < 9; depth++, node = node.parentElement) {
                if (hasTargetControls(node)) return node;
            }
        }

        // Strategy B: Dedicated <form>, <section>, <fieldset>, or role="form" containing all three controls
        const forms = Array.from(document.querySelectorAll('form, [role="form"], section, fieldset, main'))
            .filter(f => visible(f) && hasTargetControls(f));

        if (forms.length > 0) return forms[0];

        // Strategy C: Fallback to document.body
        return document.body;
        """;

    // JavaScript executing multi-tiered heuristic search for a specific control kind
    private const string FindControlsScript = """
        const root = arguments[0];
        const kind = arguments[1];
        const rawAliases = arguments[2];

        const norm = v => (v || '').replace(/\s+/g, ' ').trim().toLowerCase();
        const clean = v => norm(v)
            .replace(/[*:\(\)\[\]#]/g, ' ')
            .replace(/\b(required|optional|enter|select|choose|please)\b/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();

        const aliases = rawAliases.map(norm);
        const cleanedAliases = rawAliases.map(clean).filter(Boolean);

        const selector = kind === 'text'
            ? "input[type='text'], input:not([type]), textarea, [role='textbox']"
            : kind === 'radio'
                ? "input[type='radio'], [role='radio']"
                : kind === 'select'
                    ? "select, [role='combobox']"
                    : '';

        if (!selector) throw new Error('Unsupported control kind: ' + kind);

        const usable = el => {
            if (!el) return false;
            const style = getComputedStyle(el);
            const displayed = el.getClientRects().length > 0 && style.display !== 'none' && style.visibility !== 'hidden';
            return displayed && (kind === 'select' || !el.disabled);
        };

        const textMatches = (text) => {
            if (!text) return false;
            const n = norm(text);
            const c = clean(text);
            return aliases.includes(n) || cleanedAliases.includes(c);
        };

        const namesFor = el => {
            const labels = el.labels ? Array.from(el.labels).map(x => x.innerText) : [];
            const labelledBy = (el.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean)
                .map(id => document.getElementById(id)?.innerText || '');
            
            // Look for label or span with for="id"
            let forText = null;
            if (el.id) {
                const forEl = root.querySelector(`[for='${CSS.escape(el.id)}']`);
                if (forEl) forText = forEl.innerText;
            }

            const accessible = [
                ...labels,
                forText,
                el.closest('label')?.innerText,
                el.getAttribute('aria-label'),
                ...labelledBy
            ].filter(Boolean);

            const fallback = [
                el.getAttribute('placeholder'),
                el.getAttribute('title'),
                kind === 'radio' ? el.getAttribute('value') : null
            ].filter(Boolean);

            return { accessible, fallback };
        };

        const candidates = Array.from(root.querySelectorAll(selector)).filter(usable);

        // Tier 1: Explicit labels and ARIA accessible names
        let matches = new Set(candidates.filter(el =>
            namesFor(el).accessible.some(textMatches)));

        // Tier 2: Fallback attributes (placeholder, title, radio value)
        if (matches.size === 0) {
            matches = new Set(candidates.filter(el =>
                namesFor(el).fallback.some(textMatches)));
        }

        // Tier 3: Visual label anchors & DOM proximity
        if (matches.size === 0) {
            const anchors = Array.from(root.querySelectorAll('label, legend, span, b, strong, p, div'))
                .filter(el => usable(el) && textMatches(el.innerText));

            for (const anchor of anchors) {
                if (anchor.matches('label') && anchor.control && usable(anchor.control)) {
                    matches.add(anchor.control);
                }
                for (const nested of anchor.querySelectorAll(selector)) {
                    if (usable(nested)) matches.add(nested);
                }
                for (const sibling of [anchor.previousElementSibling, anchor.nextElementSibling]) {
                    if (sibling && sibling.matches(selector) && usable(sibling)) {
                        matches.add(sibling);
                    }
                }
                let parent = anchor.parentElement;
                for (let depth = 0; parent && depth < 3; depth++, parent = parent.parentElement) {
                    const nearby = Array.from(parent.querySelectorAll(selector)).filter(usable);
                    if (nearby.length === 1) {
                        matches.add(nearby[0]);
                        break;
                    }
                }
            }
        }

        return Array.from(matches);
        """;

    public SemanticControlFinder(IWebDriver driver, WebDriverWait wait)
    {
        _driver = driver;
        _wait = wait;
    }

    /// <summary>
    /// Finds a single, unambiguous control matching the given kind and semantic aliases.
    /// Throws an InvalidOperationException if ambiguous duplicates are detected, preventing false-positive test passes.
    /// </summary>
    public IWebElement FindUnique(string kind, params string[] aliases)
    {
        var root = _wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript(FindPrimaryRootScript) as IWebElement);
        if (root is null)
        {
            throw new InvalidOperationException("Could not locate a valid form container on the page.");
        }

        return _wait.Until(d =>
        {
            var raw = ((IJavaScriptExecutor)d).ExecuteScript(FindControlsScript, root, kind, aliases);
            var matches = raw is IEnumerable<object> items
                ? items.OfType<IWebElement>().ToList()
                : new List<IWebElement>();

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Ambiguous {kind} control for [{string.Join(", ", aliases)}]: found {matches.Count} matching elements. " +
                    "Refine semantic aliases or verify the page structure to resolve ambiguity.");
            }

            return matches.Count == 1 ? matches[0] : null;
        })!;
    }
}
