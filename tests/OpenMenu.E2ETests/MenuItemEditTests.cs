using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// Gallery-style menu item editor: photos managed as thumbnails (first = cover),
/// per-culture translation tabs, no raw "ImageUrl" field. Each test creates its
/// own data (prefix "Photo ") so the fixture cleanup removes it afterwards.
/// </summary>
public class MenuItemEditTests : E2ETestBase
{
    public MenuItemEditTests(PlaywrightFixture fixture) : base(fixture) { }

    private static readonly byte[] Png1 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    private static readonly byte[] Png2 = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private async Task<string> CreateItemAsync()
    {
        var name = $"Photo Item {Guid.NewGuid():N}".Substring(0, 16);
        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Price").FillAsync("5.00");
        return name;
    }

    private async Task SaveItemAsync()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
    }

    [Fact]
    public async Task GalleryUx_UploadCoverAndSecond_ThenRemove_Second_Persists()
    {
        await LoginAsAdminAsync();
        var name = await CreateItemAsync();

        // The editor exposes a gallery, not a raw ImageUrl field.
        await Expect(Page.GetByText("Photos", new() { Exact = true })).ToBeVisibleAsync();
        var rawField = Page.GetByLabel("Image URL");
        await Expect(rawField).ToHaveCountAsync(0);

        // Upload two photos before saving (pending gallery).
        var p1 = Path.Combine(Path.GetTempPath(), $"a-{Guid.NewGuid():N}.png");
        var p2 = Path.Combine(Path.GetTempPath(), $"b-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(p1, Png1);
        await File.WriteAllBytesAsync(p2, Png2);
        await Page.SetInputFilesAsync("input[type='file']", new[] { p1, p2 });

        var thumbs = Page.Locator("[data-gallery] img");
        await Expect(thumbs).ToHaveCountAsync(2, new() { Timeout = 15000 });
        // First thumbnail is the cover.
        await Expect(Page.Locator("[data-gallery] [data-cover]")).ToHaveCountAsync(1);

        // Remove the second photo (with confirmation). One handler for the whole
        // test; a TCS tells us the dialog actually opened before flipping the
        // accept flag, otherwise the first dialog races and gets accepted.
        var acceptNext = false;
        var lastMessage = new TaskCompletionSource<string>();
        Page.Dialog += (_, d) =>
        {
            lastMessage.TrySetResult(d.Message);
            if (acceptNext) { _ = d.AcceptAsync(); } else { _ = d.DismissAsync(); }
        };
        var removeBtn = Page.Locator("[data-gallery] [data-photo]").Nth(1)
            .GetByRole(AriaRole.Button, new() { Name = "Remove photo" });

        await removeBtn.ClickAsync();
        Assert.Contains("Delete this image", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        // Dismissed — both photos survive.
        await Expect(thumbs).ToHaveCountAsync(2, new() { Timeout = 10000 });

        acceptNext = true;
        lastMessage = new TaskCompletionSource<string>();
        await removeBtn.ClickAsync();
        Assert.Contains("Delete this image", await lastMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await Expect(thumbs).ToHaveCountAsync(1, new() { Timeout = 10000 });

        // Save and reopen: one cover photo persisted.
        await SaveItemAsync();
        await Page.GetByRole(AriaRole.Row, new() { Name = name })
            .GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await WaitForBlazorReadyAsync();
        await Expect(Page.Locator("[data-gallery] img")).ToHaveCountAsync(1, new() { Timeout = 15000 });
    }

    [Fact]
    public async Task TranslationTabs_FaNameAndPrice_SavePersistAcrossTabSwitch()
    {
        await LoginAsAdminAsync();
        var name = await CreateItemAsync();
        await SaveItemAsync();

        await Page.GetByRole(AriaRole.Row, new() { Name = name })
            .GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await WaitForBlazorReadyAsync();

        // Switch to the Farsi tab and save a translation.
        await Page.GetByRole(AriaRole.Tab, new() { Name = "فارسی" }).ClickAsync();
        var faName = Page.GetByLabel("Name (فارسی)");
        await faName.FillAsync("صورة اختبار");
        // "99.50" is already 2 decimals (step=0.01), so the browser does not
        // reformat it on re-render ("99000" would come back as "99000.00").
        await Page.GetByLabel("Price (فارسی)").FillAsync("99.50");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save translation" }).ClickAsync();

        // Switch away and back: values persist.
        await Page.GetByRole(AriaRole.Tab, new() { Name = "English" }).ClickAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "فارسی" }).ClickAsync();
        await Expect(Page.GetByLabel("Name (فارسی)")).ToHaveValueAsync("صورة اختبار", new() { Timeout = 10000 });
        await Expect(Page.GetByLabel("Price (فارسی)")).ToHaveValueAsync("99.50");
    }
}
