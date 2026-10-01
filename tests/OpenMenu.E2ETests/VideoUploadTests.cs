using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OpenMenu.E2ETests;

/// <summary>
/// Video support end to end: upload a video on the edit page, see it in the
/// editor, and find it served on the public detail page. The payload is dummy
/// bytes — the endpoint validates content type and size, and the assertions
/// check the element and its URL's HTTP status, not codec playback.
/// </summary>
public class VideoUploadTests : E2ETestBase
{
    public VideoUploadTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UploadVideo_OnEditPage_ShowsInEditor_AndOnDetailPage()
    {
        await LoginAsAdminAsync();
        var name = $"Detail Vid {Guid.NewGuid():N}".Substring(0, 14);

        // Create the item first (video attaches only to saved items).
        await Page.GotoAsync("/admin/menu/new");
        await WaitForBlazorReadyAsync();
        await WaitForCategoryOptionsAsync();
        await Page.GetByLabel("Category").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Name").FillAsync(name);
        await Page.GetByLabel("Price").FillAsync("120000");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync("**/admin/menu");
        await WaitForBlazorReadyAsync();

        // Edit: upload a video.
        await Page.GetByRole(AriaRole.Row, new() { Name = name })
            .GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await WaitForBlazorReadyAsync();
        await Page.SetInputFilesAsync("input[type='file'][accept='video/*']",
            new[] { new FilePayload
            {
                Name = "clip.mp4",
                MimeType = "video/mp4",
                Buffer = Convert.FromBase64String("AAAAIGZ0eXBpc29tAAACAGlzb21pc28yYXZjMW1wNDE="),
            } });

        // The editor shows the uploaded video element.
        var editorVideo = Page.Locator("video[data-video-editor]");
        await Expect(editorVideo).ToBeVisibleAsync(new() { Timeout = 15000 });
        var videoSrc = await editorVideo.GetAttributeAsync("src");
        Assert.NotNull(videoSrc);
        Assert.StartsWith("/media/videos/", videoSrc);

        // The media endpoint serves it (200, video content type). The page's
        // API request context resolves the relative URL against the base URL.
        var response = await Page.APIRequest.GetAsync(videoSrc);
        Assert.True(response.Ok);
        Assert.Equal("video/mp4", response.Headers["content-type"]);

        // The public detail page embeds the same video as the active gallery
        // slide (the item has no photos, so the video is slide 0).
        await Page.GotoAsync("/");
        await WaitForBlazorReadyAsync();
        var card = Page.Locator("article").Filter(new() { HasText = name });
        await card.GetByRole(AriaRole.Link, new() { Name = "Details" }).ClickAsync();
        await Page.WaitForURLAsync("**/menu/item/**");
        await WaitForBlazorReadyAsync();
        var detailVideo = Page.Locator("[data-gallery-main] video");
        await Expect(detailVideo).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
