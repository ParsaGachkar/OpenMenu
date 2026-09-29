using Microsoft.Playwright;

namespace OpenMenu.E2ETests;

/// <summary>
/// One browser for the whole suite; each test class gets a fresh browser context.
/// Uses the installed Chrome via the "chrome" channel when available (no browser
/// download needed), falling back to the bundled Chromium.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    // Keep the suite fast: anything slower than ~20s is a bug, not a wait.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public string BaseUrl =>
        Environment.GetEnvironmentVariable("OPENMENU_BASEURL") ?? "http://localhost:8088";

    public IBrowserContext NewContext() =>
        _browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = BaseUrl }).GetAwaiter().GetResult();

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        try
        {
            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Channel = "chrome" });
        }
        catch (PlaywrightException)
        {
            _browser = await _playwright.Chromium.LaunchAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

[CollectionDefinition("e2e")]
public sealed class E2ECollection : ICollectionFixture<PlaywrightFixture>;
