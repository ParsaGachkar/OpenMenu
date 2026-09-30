using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using OpenMenu.Infrastructure.Data;

namespace OpenMenu.E2ETests;

/// <summary>
/// One browser for the whole suite; each test class gets a fresh browser context.
/// Uses the installed Chrome via the "chrome" channel when available (no browser
/// download needed), falling back to the bundled Chromium.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    // Keep the suite fast: anything slower than ~20s is a bug, not a wait.
    // CI runners boot WebAssembly much slower than dev machines; OPENMENU_TIMEOUT
    // lets CI raise the ceiling (seconds) instead of failing every admin test.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(
        double.TryParse(Environment.GetEnvironmentVariable("OPENMENU_TIMEOUT"), out var s) ? s : 20);

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
        await CleanDatabaseAsync();
    }

    /// <summary>E2E rows are removed after the run so the DB stays clean.</summary>
    private static async Task CleanDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("OPENMENU_DB")
                ?? "Host=localhost;Port=5433;Database=openmenu;Username=postgres;Password=postgres")
            .Options;
        await using var db = new AppDbContext(options);
        await db.CleanE2EDataAsync();
    }
}

[CollectionDefinition("e2e")]
public sealed class E2ECollection : ICollectionFixture<PlaywrightFixture>;
