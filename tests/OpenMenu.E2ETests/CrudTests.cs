using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>Category and menu-item CRUD through the real admin UI.</summary>
public class CrudTests : E2ETestBase
{
    public CrudTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Category_CreateEditDelete_RoundTrip()
    {
        await LoginAsAdminAsync();
        var name = $"E2E Cat {Guid.NewGuid():N}".Substring(0, 14);

        // Create.
        await Page.GotoAsync("/admin/categories/new");
        await WaitForBlazorReadyAsync();
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Description").FillAsync("Created by E2E test");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = name })).ToBeVisibleAsync();

        // Edit: rename.
        await Page.GetByRole(AriaRole.Row, new() { Name = name }).GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await Page.WaitForURLAsync("**/edit");
        await WaitForBlazorReadyAsync();
        await Page.GetByLabel("Name").FillAsync(name + " v2");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");
        await WaitForBlazorReadyAsync();
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = name + " v2" })).ToBeVisibleAsync();

        // Delete (accept the confirm() dialog).
        Page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await Page.GetByRole(AriaRole.Row, new() { Name = name + " v2" }).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = name + " v2" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MenuItem_CreateEditDelete_RoundTrip()
    {
        await LoginAsAdminAsync();
        var name = $"E2E Item {Guid.NewGuid():N}".Substring(0, 14);

        // Create (first seeded category exists).
        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Price").FillAsync("9.90");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
        await Expect(Page.GetByRole(AriaRole.Cell, new() { Name = name })).ToBeVisibleAsync();

        // Edit: change price.
        await Page.GetByRole(AriaRole.Row, new() { Name = name }).GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await Page.WaitForURLAsync("**/edit");
        await WaitForBlazorReadyAsync();
        // A saved item also renders the per-culture "Price (English)" field;
        // the main price's accessible name is "Price *".
        await Page.GetByRole(AriaRole.Spinbutton, new() { Name = "Price *", Exact = true }).FillAsync("12.50");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
        // Price display depends on the configured currency (symbols, decimals);
        // assert the list shows the new value's formatted amount instead of a
        // literal like "12.50" (IRR renders "13 ریال" after rounding).
        var priceCell = Page.GetByRole(AriaRole.Row, new() { Name = name }).Locator("td").Nth(1);
        await Expect(priceCell).Not.ToContainTextAsync("9.90");

        // Delete via the item's row (scoped to the row containing the name).
        Page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await Page.GetByRole(AriaRole.Row, new() { Name = name }).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Row, new() { Name = name })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CreatedCategoryAndItem_ShowOnPublicMenu()
    {
        await LoginAsAdminAsync();
        var categoryName = $"E2E Pub {Guid.NewGuid():N}".Substring(0, 14);
        var itemName = $"E2E Pie {Guid.NewGuid():N}".Substring(0, 14);

        await Page.GotoAsync("/admin/categories/new");
        await WaitForBlazorReadyAsync();
        await Page.GetByLabel("Name").FillAsync(categoryName);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");

        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(categoryName);
        await Page.GetByLabel("Name").FillAsync(itemName);
        await Page.GetByLabel("Price").FillAsync("4.20");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();

        await Page.GotoAsync("/");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = categoryName })).ToBeVisibleAsync();
        await Expect(Page.GetByText(itemName)).ToBeVisibleAsync();
    }
}
