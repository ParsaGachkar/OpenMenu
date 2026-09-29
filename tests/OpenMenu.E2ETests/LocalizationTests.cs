using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>Localization: culture switcher, RTL, translated UI strings.</summary>
public class LocalizationTests : E2ETestBase
{
    public LocalizationTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CultureSwitcher_SwitchesToFarsi_AndAppliesRtl()
    {
        await GotoAndWaitForCircuitAsync("/");

        await SwitchCultureAsync("fa");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("dir", "rtl");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByText("ورود کارکنان")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task FarsiStrings_PersistAcrossNavigation()
    {
        await GotoAndWaitForCircuitAsync("/");
        await SwitchCultureAsync("fa");

        // Cookie-based culture survives navigation.
        await Page.GotoAsync("/login");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ورود کارکنان" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Turkish_And_Arabic_TranslationsRender()
    {
        await GotoAndWaitForCircuitAsync("/");
        await SwitchCultureAsync("tr");
        await Expect(Page.GetByText("Personel girişi")).ToBeVisibleAsync();

        await Page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await SwitchCultureAsync("ar");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("dir", "rtl");
        await Expect(Page.GetByText("دخول الموظفين")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_CanEditSettings_AndThemeApplies()
    {
        var theme = "dracula"; // A real DaisyUI theme offered by the select.
        var name = $"E2E Restaurant {Random.Shared.Next(1000, 9999)}";

        await LoginAsAdminAsync();
        await Page.GotoAsync("/admin/settings");

        await Page.GetByLabel("Restaurant name").FillAsync(name);
        // Exact: the page also has a "Primary color override" control whose
        // label text contains "theme", which would trip strict mode.
        await Page.GetByLabel("Theme", new() { Exact = true }).SelectOptionAsync(theme);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        // Save redirects to ?saved=1 so the confirmation survives the theme reload.
        await Page.WaitForURLAsync("**/admin/settings?saved=1");
        await Expect(Page.GetByText("Settings saved.")).ToBeVisibleAsync();

        // The saved theme is applied on the public site.
        await Page.GotoAsync("/");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", theme);
    }

    [Fact]
    public async Task ValidationMessages_AreLocalized()
    {
        await TestDb.EnsureUsersAsync();

        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync(TestDb.EditorUsername);
        await Page.GetByLabel("Password").FillAsync(TestDb.EditorPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin");

        // Submit an empty category form: client-side validation shows the
        // localized required-field message immediately (English default).
        await Page.GotoAsync("/admin/categories/new");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Expect(Page.GetByText("This field is required.")).ToBeVisibleAsync();

        // Switch to Farsi and check the localized message again.
        await SwitchCultureAsync("fa");
        await Page.GetByRole(AriaRole.Button, new() { Name = "ذخیره" }).ClickAsync();
        await Expect(Page.GetByText("این فیلد الزامی است.")).ToBeVisibleAsync();
    }
}
