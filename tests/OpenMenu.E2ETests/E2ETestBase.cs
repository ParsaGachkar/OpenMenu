using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

// ReSharper disable UnusedMember.Global

namespace OpenMenu.E2ETests;

/// <summary>Base class: fresh browser context + page per test, plus shared helpers.</summary>
[Collection("e2e")]
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private readonly List<string> _consoleErrors = [];

    protected E2ETestBase(PlaywrightFixture fixture) => _fixture = fixture;

    protected IPage Page { get; private set; } = null!;

    /// <summary>
    /// Console errors and uncaught page exceptions captured so far. A test that
    /// fails on a timeout should assert this is empty first — a dead WASM circuit
    /// shows up here long before any locator times out.
    /// </summary>
    protected IReadOnlyList<string> ConsoleErrors => _consoleErrors;

    public async Task InitializeAsync()
    {
        _context = _fixture.NewContext();
        // Context-level timeout also covers Expect() assertions, whose 5s default
        // is too short for WASM admin pages (framework boot + data fetch).
        _context.SetDefaultTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
        Page.SetDefaultNavigationTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                _consoleErrors.Add(msg.Text);
            }
        };
        Page.PageError += (_, ex) => _consoleErrors.Add("PAGEERROR: " + ex);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    /// <summary>
    /// Navigates and waits for the network to go idle, which for this app means
    /// the Blazor Server circuit of the interactive culture-selector island has
    /// connected — safe to then interact with the selector.
    /// </summary>
    protected Task GotoAndWaitForCircuitAsync(string url) =>
        Page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });

    /// <summary>
    /// Waits until Blazor interactivity is attached. Without this, a click on an
    /// SSR-rendered button can be swallowed mid-attach (observed: Save clicks
    /// lost on /admin/categories/new), so tests that interact with a form right
    /// after navigation must call this first. Waits for the Blazor JS to load,
    /// then a short stabilization for the server circuit to connect.
    /// </summary>
    protected async Task WaitForBlazorReadyAsync()
    {
        // "Blazor defined" only means the script loaded; clicks and JSInterop
        // need the WASM runtime (and for Auto pages the circuit) to be live.
        // Poll for the runtime marker, then give rendering a settle window.
        await Page.WaitForFunctionAsync(
            "() => typeof Blazor !== 'undefined' && (Blazor.runtime !== undefined || Blazor._internal !== undefined)",
            null, new() { Timeout = 20000 });
        await Page.WaitForTimeoutAsync(700);
    }

    /// <summary>
    /// The category select renders before its options arrive (client-side data
    /// fetch), so wait until real options exist beyond the disabled placeholder.
    /// </summary>
    protected Task WaitForCategoryOptionsAsync() =>
        Page.WaitForFunctionAsync("() => document.querySelectorAll('select option').length > 1");

    /// <summary>
    /// Selects a culture through the DaisyUI language dropdown and waits until the
    /// document actually reflects it — the switch goes through a /culture/set
    /// redirect, and the new document's lang attribute is the authoritative "done"
    /// signal (retry-safe across the navigation, unlike URL checks that are true
    /// before the redirect starts). Works on both the public layout and the admin
    /// layout, which each render exactly one dropdown.
    /// </summary>
    protected async Task SwitchCultureAsync(string culture)
    {
        var nativeNames = new Dictionary<string, string>
        {
            ["en"] = "English",
            ["fa"] = "فارسی",
            ["tr"] = "Türkçe",
            ["ar"] = "العربية",
        };

        await Page.Locator(".dropdown > [role='button']").ClickAsync();
        // Culture options are plain anchors to /culture/set (no JS needed).
        await Page.Locator(".dropdown-content")
            .GetByRole(AriaRole.Link, new() { Name = nativeNames[culture] })
            .ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", culture);
    }

    /// <summary>Logs in through the real /login form as the seeded admin.</summary>
    protected async Task LoginAsAdminAsync(string username = "admin", string password = "admin123")
    {
        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync(username);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToBeVisibleAsync();
    }
}
