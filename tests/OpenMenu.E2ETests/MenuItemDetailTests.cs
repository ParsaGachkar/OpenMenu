using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using OpenMenu.Infrastructure.Data;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// Public menu item detail page (/menu/item/{id}): the home card links to it,
/// and it shows the full gallery and the price. Runs with Currency=IRR (the
/// Iranian-restaurant scenario) to pin the whole-number "99000 ریال" format —
/// no stray ".00" cents. Each test creates its own item (prefix "Detail ")
/// so the fixture cleanup removes it.
/// </summary>
public class MenuItemDetailTests : E2ETestBase
{
    public MenuItemDetailTests(PlaywrightFixture fixture) : base(fixture) { }

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static string ConnectionString() =>
        Environment.GetEnvironmentVariable("OPENMENU_DB")
        ?? "Host=localhost;Port=5433;Database=openmenu;Username=postgres;Password=postgres";

    [Fact]
    public async Task HomeCard_LinksToDetailPage_WhichShowsGalleryAndWholeNumberIrrPrice()
    {
        // Arrange: IRR currency for the whole-number price assertions.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString()).Options;
        await using (var db = new AppDbContext(options))
        {
            var settings = await db.RestaurantSettings.FirstAsync();
            _originalCurrency = settings.Currency;
            settings.Currency = "IRR";
            await db.SaveChangesAsync();
        }

        try
        {
            await LoginAsAdminAsync();
            var name = $"Detail Item {Guid.NewGuid():N}".Substring(0, 15);

            // Create an item through the admin UI.
            await Page.GotoAsync("/admin/menu/new");
            await WaitForBlazorReadyAsync();
            await WaitForCategoryOptionsAsync();
            await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.GetByLabel("Name").FillAsync(name);
            await Page.GetByLabel("Price").FillAsync("99000");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
            await Page.WaitForURLAsync("**/admin/menu");
            await WaitForBlazorReadyAsync();

            // Upload two gallery photos on the edit page (cover + one more,
            // so the detail page has thumbnails to show).
            await Page.GetByRole(AriaRole.Row, new() { Name = name })
                .GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
            await WaitForBlazorReadyAsync();
            var photo1 = Path.Combine(Path.GetTempPath(), $"detail1-{Guid.NewGuid():N}.png");
            var photo2 = Path.Combine(Path.GetTempPath(), $"detail2-{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(photo1, Png);
            await File.WriteAllBytesAsync(photo2, Png);
            await Page.SetInputFilesAsync("input[type='file']", new[] { photo1, photo2 });
            await Expect(Page.Locator("[data-gallery] img")).ToHaveCountAsync(2, new() { Timeout = 15000 });

            // Pending photos persist only after saving the item.
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
            await Page.WaitForURLAsync("**/admin/menu");

            // Act: the public home card links to the detail page.
            await Page.GotoAsync("/");
            await WaitForBlazorReadyAsync();
            var card = Page.Locator("article").Filter(new() { HasText = name });
            await Expect(card).ToHaveCountAsync(1);
            await card.GetByRole(AriaRole.Link, new() { Name = "Details" }).ClickAsync();
            await Page.WaitForURLAsync("**/menu/item/**");

            // Assert: name, cover + gallery thumbnails, and a whole-number
            // IRR price (no cents).
            await WaitForBlazorReadyAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
            await Expect(Page.Locator("[data-gallery-main] img")).ToBeVisibleAsync();
            await Expect(Page.Locator("[data-gallery-thumbs] [data-thumb]")).ToHaveCountAsync(2);
            var price = await Page.Locator("[data-price]").InnerTextAsync();
            Assert.Contains("ریال", price);
            Assert.DoesNotContain(".00", price);
        }
        finally
        {
            await using var db = new AppDbContext(options);
            var settings = await db.RestaurantSettings.FirstAsync();
            settings.Currency = _originalCurrency;
            await db.SaveChangesAsync();
        }
    }

    private string _originalCurrency = "USD";
}
