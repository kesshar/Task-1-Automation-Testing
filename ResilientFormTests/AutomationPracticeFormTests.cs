using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace ResilientFormTests;

[TestFixture]
public sealed class AutomationPracticeFormTests
{
    private IWebDriver? _driver;
    private WebDriverWait? _wait;

    private const string DefaultUrl = "http://app.cloudqa.io/home/AutomationPracticeForm";

    [OneTimeSetUp]
    public void GlobalSetUp()
    {
        // Automatically discover and load .env file if present
        EnvLoader.Load();
    }

    [SetUp]
    public void SetUp()
    {
        var url = Environment.GetEnvironmentVariable("FORM_URL") ?? DefaultUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"FORM_URL '{url}' is invalid. It must be an absolute HTTP or HTTPS URL.");
        }

        var options = new ChromeOptions();
        options.PageLoadStrategy = PageLoadStrategy.Eager;
        options.AddArgument("--window-size=1440,1000");
        options.AddArgument("--disable-notifications");
        options.AddArgument("--disable-search-engine-choice-screen");
        options.AddArgument("--disable-popup-blocking");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");

        if (Environment.GetEnvironmentVariable("HEADLESS") == "1")
        {
            options.AddArgument("--headless=new");
        }

        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;
        _driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(180));
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(120);
        _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        _driver.Navigate().GoToUrl(url);

        // Wait until document DOM is interactive or complete
        _wait.Until(d =>
        {
            try
            {
                var state = ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString();
                return state is "interactive" or "complete";
            }
            catch
            {
                return false;
            }
        });
    }

    [Test]
    [Description("Tests First Name (text box), Gender (radio button), and State (dropdown) using resilient semantic locators.")]
    public void MainForm_FirstNameGenderAndStateCanBeTested()
    {
        var driver = _driver ?? throw new InvalidOperationException("Browser was not initialized.");
        var wait = _wait ?? throw new InvalidOperationException("Wait helper was not initialized.");
        var page = new AutomationPracticeFormPage(driver, wait);

        // Optional precondition: enter Country if required by target page version
        var country = Environment.GetEnvironmentVariable("TEST_COUNTRY");
        if (!string.IsNullOrWhiteSpace(country))
        {
            page.EnterCountry(country);
        }

        // 1. First Name (text box)
        var testFirstName = Environment.GetEnvironmentVariable("TEST_FIRST_NAME");
        if (string.IsNullOrWhiteSpace(testFirstName))
        {
            testFirstName = "John";
        }

        page.EnterFirstName(testFirstName);
        var actualFirstName = page.GetFirstName();
        Assert.That(actualFirstName, Is.EqualTo(testFirstName), "First Name value was not properly retained in the field.");

        // 2. Gender (radio button)
        var targetGender = Environment.GetEnvironmentVariable("TEST_GENDER") ?? "Male";
        page.SelectGender(targetGender);
        var isSelected = page.IsGenderSelected(targetGender);
        Assert.That(isSelected, Is.True, $"Gender radio button for '{targetGender}' was not selected.");

        // 3. State (dropdown)
        var requestedState = Environment.GetEnvironmentVariable("TEST_STATE");
        var selectedState = page.SelectState(requestedState);
        Assert.That(selectedState, Is.Not.Empty, "No State option was selected.");

        if (!string.IsNullOrWhiteSpace(requestedState))
        {
            Assert.That(selectedState, Is.EqualTo(requestedState).IgnoreCase,
                $"Expected State '{requestedState}' to be selected, but found '{selectedState}'.");
        }

        TestContext.Progress.WriteLine($"[PASS] First Name: {testFirstName}");
        TestContext.Progress.WriteLine($"[PASS] Gender: {targetGender}");
        TestContext.Progress.WriteLine($"[PASS] State: {selectedState}");
    }

    [TearDown]
    public void TearDown()
    {
        if (_driver is null) return;

        try
        {
            if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Passed)
            {
                Directory.CreateDirectory("artifacts");
                var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
                var screenshotPath = Path.Combine("artifacts", "failure.png");
                screenshot.SaveAsFile(screenshotPath);
                TestContext.Progress.WriteLine($"[FAILURE EVIDENCE] Screenshot saved to: {screenshotPath}");
            }
        }
        catch
        {
            // Do not override primary test failure if screenshot capture encounters an error
        }
        finally
        {
            _driver.Quit();
            _driver.Dispose();
            _driver = null;
        }
    }
}
