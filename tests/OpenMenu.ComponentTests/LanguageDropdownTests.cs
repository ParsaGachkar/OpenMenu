using OpenMenu.Web.Client.Layout;

namespace OpenMenu.ComponentTests;

/// <summary>
/// LanguageDropdown contract tests against the real component: culture
/// filtering, /culture/set links and the data-enhance-nav="false" attribute
/// (regression: enhanced navigation kept the WASM runtime on the old culture
/// until a manual refresh).
/// </summary>
public class LanguageDropdownTests : TestContext
{
    [Fact]
    public void Shows_AllCultures_WhenNoFilterProvided()
    {
        var cut = RenderComponent<LanguageDropdown>();

        var links = cut.FindAll("a");
        Assert.Equal(4, links.Count);
        Assert.Contains(links, a => a.TextContent.Contains("English"));
        Assert.Contains(links, a => a.TextContent.Contains("فارسی"));
        Assert.Contains(links, a => a.TextContent.Contains("Türkçe"));
        Assert.Contains(links, a => a.TextContent.Contains("العربية"));
    }

    [Fact]
    public void Shows_OnlyEnabledCultures_WhenFilterProvided()
    {
        var cut = RenderComponent<LanguageDropdown>(
            p => p.Add(d => d.EnabledCultures, ["en", "fa"]));

        var links = cut.FindAll("a");
        Assert.Equal(2, links.Count);
        Assert.Contains(links, a => a.TextContent.Contains("English"));
        Assert.Contains(links, a => a.TextContent.Contains("فارسی"));
    }

    [Fact]
    public void FallsBackToAllCultures_WhenFilterWouldHideEverything()
    {
        var cut = RenderComponent<LanguageDropdown>(
            p => p.Add(d => d.EnabledCultures, ["xx"]));

        Assert.Equal(4, cut.FindAll("a").Count);
    }

    [Fact]
    public void CultureLinks_PointToCultureSet_WithCurrentPageAsRedirect()
    {
        var cut = RenderComponent<LanguageDropdown>();

        var faLink = cut.FindAll("a").Single(a => a.TextContent.Contains("فارسی"));
        var href = faLink.GetAttribute("href");
        Assert.StartsWith("/culture/set?culture=fa", href);
        Assert.Contains("redirectTo=%2F", href);
    }

    [Fact]
    public void CultureLinks_DisableEnhancedNavigation()
    {
        // Regression: with enhanced navigation the culture cookie changed but a
        // running WebAssembly runtime kept the old language until manual refresh.
        var cut = RenderComponent<LanguageDropdown>();

        foreach (var link in cut.FindAll("a"))
        {
            Assert.Equal("false", link.GetAttribute("data-enhance-nav"));
        }
    }
}
