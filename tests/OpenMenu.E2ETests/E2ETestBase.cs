using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>Base class: fresh browser context + page per test, plus shared helpers.</summary>
[Collection("e2e")]
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;

    protected E2ETestBase(PlaywrightFixture fixture) => _fixture = fixture;

    protected IPage Page { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _context = _fixture.NewContext();
        // Context-level timeout also covers Expect() assertions, whose 5s default
        // is too short for WASM admin pages (framework boot + data fetch).
        _context.SetDefaultTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
        Page.SetDefaultNavigationTimeout((float)PlaywrightFixture.Timeout.TotalMilliseconds);
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
