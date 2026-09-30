using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// Bugs reported manually: (1) the language dropdown button keeps showing the
/// previous language after switching, (2) destructive actions have no
/// confirmation dialog. Each test reproduces the bug and is expected to fail
/// until fixed (TDD red), then pass (green).
/// </summary>
public class ReportedBugTests : E2ETestBase
{
    public ReportedBugTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LanguageDropdownButton_FollowsSelectedCulture()
    {
        await GotoAndWaitForCircuitAsync("/");

        // The dropdown button shows the active culture's name.
        await Expect(Page.Locator(".dropdown > [role='button']")).ToContainTextAsync("English");

        await SwitchCultureAsync("fa");

        // BUG 1: the switch works (html lang=fa, asserted inside SwitchCultureAsync)
        // but the button label stayed "English". It must follow the culture.
        await Expect(Page.Locator(".dropdown > [role='button']")).ToContainTextAsync("فارسی");
        await Expect(Page.Locator(".dropdown > [role='button']")).Not.ToContainTextAsync("English");
    }

    [Fact]
    public async Task CategoryDelete_ShowsConfirmation_AndDeletesOnlyOnAccept()
    {
        await LoginAsAdminAsync();
        var name = $"Confirm Cat {Guid.NewGuid():N}".Substring(0, 14);

        await Page.GotoAsync("/admin/categories/new");
        await WaitForBlazorReadyAsync();
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/categories");
        await WaitForBlazorReadyAsync();

        // One dialog handler; a flag decides dismiss (first) vs accept (second).
        var acceptNext = false;
        var lastMessage = new TaskCompletionSource<string>();
        Page.Dialog += (_, d) =>
        {
            lastMessage.TrySetResult(d.Message);
            if (acceptNext) { _ = d.AcceptAsync(); } else { _ = d.DismissAsync(); }
        };

        // BUG 2: clicking Delete must NOT remove the row before confirmation.
        var row = Page.GetByRole(AriaRole.Row, new() { Name = name });
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        Assert.Contains("Delete this category", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Dismissed — the category must survive.
        await Expect(row).ToBeVisibleAsync();

        // Accept — now the row disappears.
        acceptNext = true;
        lastMessage = new TaskCompletionSource<string>();
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        Assert.Contains("Delete this category", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await Expect(row).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    [Fact]
    public async Task MenuItemDelete_ShowsConfirmation_AndDeletesOnlyOnAccept()
    {
        await LoginAsAdminAsync();
        var name = $"Confirm Item {Guid.NewGuid():N}".Substring(0, 14);

        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Price").FillAsync("1.50");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();

        var row = Page.GetByRole(AriaRole.Row, new() { Name = name });
        var acceptNext = false;
        var lastMessage = new TaskCompletionSource<string>();
        Page.Dialog += (_, d) =>
        {
            lastMessage.TrySetResult(d.Message);
            if (acceptNext) { _ = d.AcceptAsync(); } else { _ = d.DismissAsync(); }
        };

        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        Assert.Contains("Delete this menu item", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Dismissed — the item must survive.
        await Expect(row).ToBeVisibleAsync();

        // Accept — the row disappears.
        acceptNext = true;
        lastMessage = new TaskCompletionSource<string>();
        await row.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        Assert.Contains("Delete this menu item", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await Expect(row).ToHaveCountAsync(0, new() { Timeout = 10000 });
    }
}
