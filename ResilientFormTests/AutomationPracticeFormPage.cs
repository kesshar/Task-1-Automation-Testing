using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ResilientFormTests;

/// <summary>
/// Page Object representing the Automation Practice Form.
/// Interacts with controls via the SemanticControlFinder rather than static selectors.
/// </summary>
public sealed class AutomationPracticeFormPage
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly SemanticControlFinder _finder;

    public AutomationPracticeFormPage(IWebDriver driver, WebDriverWait wait)
    {
        _driver = driver;
        _wait = wait;
        _finder = new SemanticControlFinder(driver, wait);
    }

    /// <summary>
    /// Enters text into the First Name field.
    /// Uses resilient aliases to accommodate minor label/placeholder changes.
    /// </summary>
    public void EnterFirstName(string value)
    {
        var input = _finder.FindUnique("text", "First Name", "FirstName", "Given Name", "Name");
        input.Clear();
        input.SendKeys(value);
    }

    /// <summary>
    /// Retrieves the current value of the First Name field.
    /// </summary>
    public string GetFirstName()
    {
        var input = _finder.FindUnique("text", "First Name", "FirstName", "Given Name", "Name");
        return input.GetAttribute("value") ?? input.Text ?? string.Empty;
    }

    /// <summary>
    /// Optional precondition: enters a Country if the page uses a cascading State dropdown.
    /// </summary>
    public void EnterCountry(string value)
    {
        var input = _finder.FindUnique("text", "Country");
        input.Clear();
        input.SendKeys(value);
    }

    /// <summary>
    /// Selects a Gender radio button by its semantic label/value (e.g. Male, Female, Transgender).
    /// Handles styled/overlayed radio controls by falling back to JavaScript invocation if click is intercepted.
    /// </summary>
    public void SelectGender(string gender)
    {
        var aliases = GetGenderAliases(gender);
        var radio = _finder.FindUnique("radio", aliases);

        if (!IsSelected(radio))
        {
            try
            {
                radio.Click();
            }
            catch (ElementClickInterceptedException)
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", radio);
            }
        }
    }

    /// <summary>
    /// Checks whether the specified Gender radio button is currently selected.
    /// </summary>
    public bool IsGenderSelected(string gender)
    {
        var aliases = GetGenderAliases(gender);
        var radio = _finder.FindUnique("radio", aliases);
        return IsSelected(radio);
    }

    /// <summary>
    /// Selects an option from the State dropdown.
    /// Works with native HTML &lt;select&gt; elements and ARIA &lt;div role="combobox"&gt; controls.
    /// If requestedState is null or empty, the first valid non-placeholder option is selected.
    /// </summary>
    public string SelectState(string? requestedState)
    {
        var control = _finder.FindUnique("select", "State", "State / Province", "Region", "Province");

        // Native HTML <select>
        if (control.TagName.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            var select = new SelectElement(control);
            try
            {
                _wait.Until(_ => control.Enabled && select.Options.Count > 1);
            }
            catch (WebDriverTimeoutException ex)
            {
                throw new InvalidOperationException(
                    "State options did not load. If this page variant requires a Country before populating State, " +
                    "set TEST_COUNTRY in your .env file.", ex);
            }

            var validOptions = select.Options
                .Where(o => o.Enabled && !IsPlaceholder(o.Text))
                .ToList();

            if (validOptions.Count == 0)
            {
                throw new InvalidOperationException("No valid, non-placeholder options found in the State dropdown.");
            }

            var target = string.IsNullOrWhiteSpace(requestedState)
                ? validOptions.First()
                : validOptions.FirstOrDefault(o =>
                    string.Equals(o.Text.Trim(), requestedState.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.GetAttribute("value")?.Trim(), requestedState.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                var available = string.Join(", ", validOptions.Select(o => $"'{o.Text.Trim()}'"));
                throw new InvalidOperationException(
                    $"Requested State '{requestedState}' was not found in the dropdown. Available options: {available}");
            }

            select.SelectByText(target.Text);
            return select.SelectedOption.Text.Trim();
        }

        // Accessible custom combobox (role="combobox")
        if (string.Equals(control.GetAttribute("role"), "combobox", StringComparison.OrdinalIgnoreCase))
        {
            control.Click();
            var options = _wait.Until(d =>
            {
                var visible = d.FindElements(By.CssSelector("[role='option']"))
                    .Where(o => o.Displayed && o.Enabled && !IsPlaceholder(o.Text))
                    .ToList();
                return visible.Count > 0 ? visible : null;
            });

            var target = string.IsNullOrWhiteSpace(requestedState)
                ? options!.First()
                : options!.FirstOrDefault(o =>
                    string.Equals(o.Text.Trim(), requestedState.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                var available = string.Join(", ", options!.Select(o => $"'{o.Text.Trim()}'"));
                throw new InvalidOperationException(
                    $"Requested State '{requestedState}' was not found in the combobox. Available options: {available}");
            }

            var chosenText = target.Text.Trim();
            target.Click();
            return chosenText;
        }

        throw new NotSupportedException($"Unsupported State control type: <{control.TagName}> with role='{control.GetAttribute("role")}'.");
    }

    private static string[] GetGenderAliases(string gender)
    {
        var trimmed = gender.Trim();
        if (string.Equals(trimmed, "male", StringComparison.OrdinalIgnoreCase))
            return ["Male", "M"];
        if (string.Equals(trimmed, "female", StringComparison.OrdinalIgnoreCase))
            return ["Female", "F"];
        return [trimmed];
    }

    private static bool IsSelected(IWebElement radio)
    {
        return radio.Selected ||
               radio.GetAttribute("checked") != null ||
               string.Equals(radio.GetAttribute("aria-checked"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholder(string text)
    {
        var normalized = new string(text.ToLowerInvariant().Where(char.IsLetter).ToArray());
        return normalized.Length == 0 ||
               normalized.StartsWith("select") ||
               normalized.StartsWith("choose") ||
               normalized.StartsWith("please");
    }
}

