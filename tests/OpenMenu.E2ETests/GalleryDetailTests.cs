using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// Media UX regressions: the home card must show ONE media (cover image, or
/// the video only when there is no image — never both), and the detail page
/// must be a gallery with an active slide in the main box where the video is
/// a first-class, switchable slide. Each test creates its own item so the
/// fixture cleanup removes it.
/// </summary>
public class GalleryDetailTests : E2ETestBase
{
    public GalleryDetailTests(PlaywrightFixture fixture) : base(fixture) { }

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private const string Mp4 = "AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDE=";

    private async Task<string> CreateItemAsync(string namePrefix)
    {
        var name = $"{namePrefix} {Guid.NewGuid():N}".Substring(0, 16);
        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Price").FillAsync("12.50");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
        return name;
    }

    private async Task OpenEditorAsync(string name)
    {
        await Page.GetByRole(AriaRole.Row, new() { Name = name })
            .GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await WaitForBlazorReadyAsync();
    }

    private async Task<string> UploadVideoAsync()
    {
        await Page.SetInputFilesAsync("input[type='file'][accept='video/*']",
            new[] { new FilePayload { Name = "clip.mp4", MimeType = "video/mp4", Buffer = Convert.FromBase64String(Mp4) } });
        var editorVideo = Page.Locator("video[data-video-editor]");
        await Expect(editorVideo).ToBeVisibleAsync(new() { Timeout = 15000 });
        return (await editorVideo.GetAttributeAsync("src"))!;
    }

    private async Task UploadPhotosAsync(int count)
    {
        var files = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(Path.GetTempPath(), $"gal-{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(path, Png);
            files.Add(path);
        }

        await Page.SetInputFilesAsync("input[type='file'][accept='image/*']", files);
        await Expect(Page.Locator("[data-gallery] img")).ToHaveCountAsync(count, new() { Timeout = 15000 });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();
    }

    private async Task OpenDetailAsync(string name)
    {
        await Page.GotoAsync("/");
        await WaitForBlazorReadyAsync();
        var card = Page.Locator("article").Filter(new() { HasText = name });
        await Expect(card).ToHaveCountAsync(1);
        await card.GetByRole(AriaRole.Link, new() { Name = "Details" }).ClickAsync();
        await Page.WaitForURLAsync("**/menu/item/**");
        await WaitForBlazorReadyAsync();
    }

    [Fact]
    public async Task DetailPage_GalleryWithVideo_ActiveSlideSwitches_InMainBox()
    {
        await LoginAsAdminAsync();
        var name = await CreateItemAsync("Gal Vid");

        // Attach one video and two photos to the saved item.
        await OpenEditorAsync(name);
        await UploadVideoAsync();
        await UploadPhotosAsync(2);

        // Act: open the public detail page.
        await OpenDetailAsync(name);

        // The video is a gallery slide and — being first — starts active in
        // the main box. Exactly one media element is shown at any time.
        var main = Page.Locator("[data-gallery-main]");
        await Expect(main.Locator("video")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(main.Locator("img")).ToHaveCountAsync(0);

        var thumbs = Page.Locator("[data-gallery-thumbs] [data-thumb]");
        await Expect(thumbs).ToHaveCountAsync(3); // video first, then the photos
        await Expect(Page.Locator("[data-gallery-thumbs] .thumb-active")).ToHaveCountAsync(1);

        // Click the first photo thumb: it becomes the active slide in the main
        // box and the video disappears from the main box.
        var photoSrc = await thumbs.Nth(1).Locator("img").GetAttributeAsync("src");
        await thumbs.Nth(1).ClickAsync();
        var mainImage = main.Locator("img[data-main-image]");
        await Expect(mainImage).ToBeVisibleAsync();
        await Expect(mainImage).ToHaveAttributeAsync("src", photoSrc!);
        await Expect(main.Locator("video")).ToHaveCountAsync(0);
        await Expect(Page.Locator("[data-gallery-thumbs] .thumb-active")).ToHaveCountAsync(1);

        // Click the second photo thumb: the main box follows.
        var photo2Src = await thumbs.Nth(2).Locator("img").GetAttributeAsync("src");
        await thumbs.Nth(2).ClickAsync();
        await Expect(mainImage).ToHaveAttributeAsync("src", photo2Src!);

        // Click the video thumb again: the video comes back into the main box.
        await thumbs.Nth(0).ClickAsync();
        await Expect(main.Locator("video")).ToBeVisibleAsync();
        await Expect(main.Locator("img")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task DetailPage_PhotoGallery_ActiveSlideSwitches_WithoutVideo()
    {
        await LoginAsAdminAsync();
        var name = await CreateItemAsync("Gal Pic");

        await OpenEditorAsync(name);
        await UploadPhotosAsync(2);

        await OpenDetailAsync(name);

        var main = Page.Locator("[data-gallery-main]");
        await Expect(main.Locator("img[data-main-image]")).ToBeVisibleAsync();
        await Expect(main.Locator("img")).ToHaveCountAsync(1);

        var thumbs = Page.Locator("[data-gallery-thumbs] [data-thumb]");
        await Expect(thumbs).ToHaveCountAsync(2);

        var photoSrc = await thumbs.Nth(1).Locator("img").GetAttributeAsync("src");
        await thumbs.Nth(1).ClickAsync();
        await Expect(main.Locator("img[data-main-image]")).ToHaveAttributeAsync("src", photoSrc!);
        await Expect(Page.Locator("[data-gallery-thumbs] .thumb-active")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task HomeCard_ShowsSingleMedia_ImageOrVideo_NeverBoth()
    {
        await LoginAsAdminAsync();

        // Item A: photos only — the card must show the image and no video.
        var withImage = await CreateItemAsync("Card Img");
        await OpenEditorAsync(withImage);
        await UploadPhotosAsync(1);

        // Item B: video only — the card must show the video and no image.
        var withVideo = await CreateItemAsync("Card Vid");
        await OpenEditorAsync(withVideo);
        await UploadVideoAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();

        await Page.GotoAsync("/");
        await WaitForBlazorReadyAsync();

        var imageCard = Page.Locator("article").Filter(new() { HasText = withImage });
        await Expect(imageCard).ToHaveCountAsync(1);
        await Expect(imageCard.Locator("img")).ToBeVisibleAsync();
        await Expect(imageCard.Locator("video")).ToHaveCountAsync(0);

        var videoCard = Page.Locator("article").Filter(new() { HasText = withVideo });
        await Expect(videoCard).ToHaveCountAsync(1);
        await Expect(videoCard.Locator("video")).ToBeVisibleAsync();
        await Expect(videoCard.Locator("img")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TranslationCurrency_FaToman_ShowsOnPublicPages_WhileEnglishKeepsDefault()
    {
        await LoginAsAdminAsync();
        var name = await CreateItemAsync("Cur Anim"); // "Cur " prefix, unique tail

        // Save a Farsi translation: price in TOMAN (restaurant stays USD).
        var faName = name + " فارسی";
        await OpenEditorAsync(name);
        await Page.GetByRole(AriaRole.Tab, new() { Name = "فارسی" }).ClickAsync();
        await Page.GetByLabel("Name (فارسی)").FillAsync(faName);
        await Page.GetByLabel("Price (فارسی)").FillAsync("99.50");
        await Page.Locator("[data-translation-currency]").SelectOptionAsync("TOMAN");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save translation" }).ClickAsync();
        await Expect(Page.GetByLabel("Name (فارسی)")).ToHaveValueAsync(faName, new() { Timeout = 10000 });

        // Public menu in Farsi: the card shows the translated price in Toman.
        await SwitchCultureAsync("fa");
        await Page.GotoAsync("/");
        await WaitForBlazorReadyAsync();
        var faCard = Page.Locator("article").Filter(new() { HasText = faName });
        await Expect(faCard).ToContainTextAsync("تومان", new() { Timeout = 10000 });

        // Detail page (fa) likewise shows Toman.
        await faCard.GetByRole(AriaRole.Link, new() { Name = "جزئیات" }).ClickAsync();
        await Page.WaitForURLAsync("**/menu/item/**");
        await WaitForBlazorReadyAsync();
        await Expect(Page.Locator("[data-price]")).ToContainTextAsync("تومان");

        // English view: no translation exists → the restaurant default currency
        // applies (whatever settings say — the dev DB is often IRR), NOT Toman.
        await Page.GotoAsync("/?culture=en");
        await WaitForBlazorReadyAsync();
        var enCard = Page.Locator("article").Filter(new() { HasText = name });
        var enPrice = await enCard.Locator("[data-price]").InnerTextAsync();
        Assert.DoesNotContain("تومان", enPrice);
        Assert.DoesNotContain("99", enPrice); // not the translated price either
        var body = await Page.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("تومان", body);
    }
}
