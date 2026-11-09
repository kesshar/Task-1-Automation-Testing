# Approach & Limitations — Resilient Form Automation

## 1. Approach Overview

The objective of this assignment is to automate interactions with three fields—**First Name (text box)**, **Gender (radio button)**, and **State (dropdown)**—such that the test remains functional even if:
- Field order is rearranged.
- HTML tag hierarchy is altered or refactored.
- `id`, `name`, or CSS class attributes are changed, obfuscated, or auto-generated.
- Similar fields exist elsewhere on the page.

### Architectural Strategy

1. **Page Object Model (POM) & Separation of Concerns**:
   - `AutomationPracticeFormTests`: Test lifecycle, NUnit assertions, configuration loading, failure screenshot capture.
   - `AutomationPracticeFormPage`: High-level domain interactions (entering names, selecting radios, picking dropdown options).
   - `SemanticControlFinder`: Low-level resilient DOM query engine executing client-side multi-tier heuristics via JavaScript.
   - `EnvLoader`: Zero-dependency `.env` parser that loads environment configurations while respecting CLI overrides.

2. **Scoped Container Discovery**:
   - Rather than querying `document` globally (which risks picking up duplicate or inactive form fragments from headers, footers, or demo sections), discovery identifies the active form container.
   - Prioritizes semantic section headings (`Form`, `Automation Practice Form`, `Registration`), followed by isolated containers (`<form>`, `[role="form"]`, `<section>`, `<fieldset>`) that contain the target control cluster (text box + radio button + dropdown).

3. **Multi-Tier Semantic Heuristics**:
   - **Tier 1 (Explicit Accessibility)**: Resolves elements using HTML `<label for="...">` associations, wrapping `<label>` tags, `aria-label`, and `aria-labelledby`.
   - **Tier 2 (Semantic Attributes)**: Evaluates `placeholder`, `title`, and input values (for radio buttons like `value="Male"`).
   - **Tier 3 (Visual & Proximity Fallback)**: Scans adjacent visual text anchors (`<span>`, `<label>`, `<legend>`, `<p>`) for sibling or parent container relationships (`.form-group`, `.field`, `.row`).
   - **Normalization & Noise Reduction**: Normalizes case, collapses whitespace, and strips punctuation/noise tokens (e.g. `First Name:`, `* First Name`, `First Name (required)`).

4. **Ambiguity Prevention**:
   - If more than one candidate matches a given semantic query, the engine throws an `InvalidOperationException` describing the ambiguity rather than guessing or selecting an arbitrary element. This prevents silent false positives.

5. **Dynamic & Asynchronous Handling**:
   - Uses Selenium `WebDriverWait` with explicit conditions instead of arbitrary sleeps.
   - Supports both native `<select>` controls and accessible custom dropdowns (`role="combobox"` / `role="option"`).
   - Accommodates CSS-styled radio overlays with automatic JavaScript click fallbacks if clicks are intercepted.

---

## 2. Limitations & Trade-Offs

While this heuristic approach provides significantly higher resilience than static XPath or CSS selectors, realistic test automation has clear boundaries:

1. **Boundary of Context (Light DOM vs. Shadow DOM / iframes)**:
   - Queries operate within the current document's Light DOM. Elements nested inside detached Shadow DOM roots or external `<iframe>` elements require explicit context-switching (`SwitchTo().Frame()`).

2. **Semantic Entropy**:
   - If a redesign removes all semantic signals simultaneously—no labels, no ARIA attributes, no placeholders, and no proximate text—pure heuristic identification cannot reliably determine field purpose without a shared contract (such as dedicated `data-testid` attributes).

3. **Homogeneous Multi-Field Ambiguity**:
   - If a form contains multiple identical controls without unique labels (for example, two identical unlabelled text boxes for "First Name" and "Middle Name" that both only say "Name"), the engine intentionally fails with an ambiguity error to prevent unintended data corruption.

4. **Dynamic Data Preconditions**:
   - On forms with dependent/cascading dropdowns (e.g., State options populated only after selecting Country), the test depends on valid test data inputs via `TEST_COUNTRY` and `TEST_STATE`.

5. **Visual Obstructions**:
   - Transparent overlays, modal backdrops, or floating cookie banners can intercept physical user clicks. The framework mitigates this via JS fallback clicks and takes full-screen screenshots on failure for post-run analysis.
