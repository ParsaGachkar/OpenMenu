using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// InteractiveAuto pages must not lose their culture when the WebAssembly
/// runtime takes over from the server prerender. Bug: strings rendered correctly
/// on the server, then flipped back to English once the page became interactive.
/// </summary>
public class AdminLocalizationTests : E2ETestBase
{
    public AdminLocalizationTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AdminUi_KeepsFarsi_AfterPageBecomesInteractive()
    {
        await LoginAsAdminAsync();
        await SwitchCultureAsync("fa");

        // WASM boots and re-renders the admin dashboard; the culture cookie must
        // be honored client-side too. Before the fix the heading flips to
        // English ("Dashboard") here.
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard" })).ToHaveCountAsync(0);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
    }

    [Fact]
    public async Task AdminCultureSwitch_ShowsNewLanguage_WithoutManualRefresh()
    {
        await LoginAsAdminAsync();

        // Start on an interactive admin page in English.
        await Page.GotoAsync("/admin/menu");
        await WaitForBlazorReadyAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Menu items" })).ToBeVisibleAsync();

        // Switch to Farsi via the dropdown. The anchor must trigger a full
        // document reload (no Blazor enhanced navigation), otherwise WASM keeps
        // running with the old culture and the page stays English until the user
        // manually refreshes.
        await SwitchCultureAsync("fa");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Menu items" })).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "آیتم‌های منو" })).ToBeVisibleAsync();
    }
}
