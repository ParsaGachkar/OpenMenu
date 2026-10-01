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

    // ---- Full admin flows in Farsi: list → new → edit. These catch two bug
    // classes: (a) culture lost on InteractiveAuto pages, and (b) hardcoded
    // English strings that never had a resx entry at all ("Manage menu",
    // empty-state texts, page titles, role options...).

    [Fact]
    public async Task Categories_ListNewEdit_Farsi_NoEnglishLeaks()
    {
        await LoginAsAdminAsync();
        await SwitchCultureAsync("fa");

        // List (navigated through the admin nav menu, a client-side NavLink).
        await Page.Locator(".menu").GetByRole(AriaRole.Link, new() { Name = "دسته‌ها" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(700);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "دسته‌ها" })).ToBeVisibleAsync();

        var listBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("New category", listBody);
        Assert.DoesNotContain("No categories yet", listBody);

        // New flow (button is a NavLink on an interactive page).
        await Page.GetByRole(AriaRole.Link, new() { Name = "دسته جدید" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories/new");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "دسته جدید" })).ToBeVisibleAsync();

        var newBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("New category", newBody);
        Assert.DoesNotContain("Cancel", newBody);

        var faName = $"دسته آزمون {Guid.NewGuid():N}"[..20];
        await Page.GetByLabel("نام").FillAsync(faName);
        await Page.GetByRole(AriaRole.Button, new() { Name = "ذخیره", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");
        await WaitForBlazorReadyAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Row, new() { Name = faName })).ToBeVisibleAsync();

        // Edit flow (row link).
        await Page.GetByRole(AriaRole.Row, new() { Name = faName })
            .GetByRole(AriaRole.Link, new() { Name = "ویرایش" }).ClickAsync();
        await Page.WaitForURLAsync("**/edit");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ویرایش دسته" })).ToBeVisibleAsync();

        var editBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("Edit category", editBody);
        Assert.DoesNotContain("Save", editBody);
    }

    [Fact]
    public async Task MenuItems_ListNewEdit_Farsi_NoEnglishLeaks()
    {
        await LoginAsAdminAsync();
        await SwitchCultureAsync("fa");

        // List.
        await Page.GotoAsync("/admin/menu");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(700);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "آیتم‌های منو" })).ToBeVisibleAsync();
        var listBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("Menu items", listBody);

        // New flow.
        await Page.GetByRole(AriaRole.Link, new() { Name = "آیتم جدید" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "آیتم جدید" })).ToBeVisibleAsync();

        var newBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("New item", newBody);
        Assert.DoesNotContain("Translations can be added after saving", newBody);

        var itemName = $"آیتم آزمون {Guid.NewGuid():N}"[..20];
        await Page.GetByLabel("دسته").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("نام").FillAsync(itemName);
        await Page.GetByLabel("قیمت").FillAsync("9.50");
        await Page.GetByRole(AriaRole.Button, new() { Name = "ذخیره", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Row, new() { Name = itemName })).ToBeVisibleAsync();

        // Edit flow (first data row of the first category table).
        await Page.Locator("tbody tr").First
            .GetByRole(AriaRole.Link, new() { Name = "ویرایش" }).ClickAsync();
        await Page.WaitForURLAsync("**/edit");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fa");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "ویرایش آیتم منو" })).ToBeVisibleAsync();

        var editBody = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("Edit menu item", editBody);
        Assert.DoesNotContain("Save translation", editBody);
    }

    [Fact]
    public async Task Dashboard_Farsi_NoHardcodedEnglishButtons()
    {
        await LoginAsAdminAsync();
        await SwitchCultureAsync("fa");
        await WaitForBlazorReadyAsync();
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "داشبورد" })).ToBeVisibleAsync();
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("Manage menu", body);
        Assert.DoesNotContain("View public menu", body);
    }
}
