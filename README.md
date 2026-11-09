# Resilient Form Automation (C# & Selenium)

Automated testing suite for the CloudQA Automation Practice Form that remains resilient against layout redesigns, attribute mutations (`id`, `name`, `class`), field reordering, and similar duplicate elements on the page.

Tested fields:
- **First Name** (Text box)
- **Gender** (Radio button)
- **State** (Dropdown / Combobox)

---

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or later)
- Google Chrome browser
- Supported OS: Windows, macOS, or Linux

*(Note: Selenium Manager automatically resolves and downloads the compatible ChromeDriver at runtime; manual driver installation is not required).*

---

## Setup & Configuration

1. Clone or navigate to the repository:
   ```bash
   cd Task-1-Automation-Testing
   ```

2. Create your `.env` configuration file from the provided template:
   ```bash
   # On Windows (PowerShell):
   Copy-Item .env.example .env

   # On macOS/Linux:
   cp .env.example .env
   ```

3. Update `.env` with your desired configuration:
   ```env
   # Target URL under test
   FORM_URL=http://app.cloudqa.io/home/AutomationPracticeForm

   # Test Data
   TEST_FIRST_NAME=John
   TEST_GENDER=Male
   TEST_STATE=United States
   TEST_COUNTRY=India

   # Headless Mode: 0 for visible browser, 1 for headless
   HEADLESS=0
   ```
   *(Note: The built-in `EnvLoader` automatically loads this `.env` file before executing tests. Command-line environment variables override `.env` values when provided).*

---

## Running the Tests

### Quick Run (Windows PowerShell)

Execute the included launcher directly from the repository root:

```powershell
.\run-tests.ps1
```

### Standard CLI Run

Execute tests directly from the repository root:

```bash
dotnet test --logger "console;verbosity=normal"
```

Or from the project directory:

```bash
cd ResilientFormTests
dotnet test --logger "console;verbosity=normal"
```

### Running with Command-Line Overrides

You can also pass environment variables directly from PowerShell / Bash without modifying `.env`:

```powershell
$env:FORM_URL = "http://app.cloudqa.io/home/AutomationPracticeForm"
$env:TEST_GENDER = "Female"
$env:TEST_STATE = "Argentina"
$env:HEADLESS = "1"
dotnet test
```

---

## Architecture & Resilience Strategy

- **Zero Hardcoded Selectors**: Does not use fragile static XPaths (e.g. `//form/div[2]/input`) or brittle IDs/classes (e.g. `#fname`, `.col-xs-6`).
- **Scoped Primary Container**: Scopes searches to the primary form container using heading heuristics and control clustering, ignoring duplicate or inert sections.
- **Multi-Tier Semantic Matching**:
  1. *Tier 1 (Accessibility)*: Native `<label for="...">` associations, wrapping `<label>` tags, `aria-label`, and `aria-labelledby`.
  2. *Tier 2 (Attribute Semantics)*: Placeholders, titles, and input values.
  3. *Tier 3 (Visual Proximity)*: Sibling and parent container text nodes (`.form-group`, `.field`).
- **Normalization & Noise Tolerance**: Strips punctuation, asterisks, colons, and noise keywords (`First Name:`, `* First Name`, `First Name (required)`).
- **Strict Ambiguity Prevention**: Fails explicitly if multiple candidates match rather than arbitrarily clicking a false-positive element.
- **Dual Dropdown Support**: Seamlessly supports both native HTML `<select>` elements and ARIA custom dropdowns (`[role="combobox"]`).
- **Failure Artifacts**: Automatically captures a full-screen screenshot to `artifacts/failure.png` if any step fails.

For a comprehensive explanation of architectural decisions and limitations, see [APPROACH_LIMITATIONS.md](APPROACH_LIMITATIONS.md).
