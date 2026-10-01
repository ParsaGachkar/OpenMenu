using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using OpenMenu.Application.Shared;
using OpenMenu.Domain;
using OpenMenu.Web.Client.Pages.Admin;

namespace OpenMenu.ComponentTests;

/// <summary>
/// Real admin editor components rendered against a fake IAdminApi (the same
/// interface the server circuit and the WASM client both implement).
/// </summary>
public abstract class AdminComponentTest : TestContext
{
    protected FakeAdminApi Api { get; } = new();

    protected AdminComponentTest()
    {
        // Real resx localizer (same as the WASM host registers), so the
        // components render their actual localized strings.
        Services.AddLocalization();
        Services.AddSingleton<IAdminApi>(Api);
    }

    /// <summary>In-memory IAdminApi double: records writes, returns canned rows.</summary>
    protected sealed class FakeAdminApi : IAdminApi
    {
        public List<CategoryDto> Categories { get; set; } = [];
        public List<MenuItem> MenuItems { get; set; } = [];
        public List<CategoryTranslation> CategoryTranslations { get; set; } = [];
        public List<MenuItemTranslation> MenuItemTranslations { get; set; } = [];
        public List<MenuItemImage> MenuItemImages { get; set; } = [];
        public List<CategoryTranslationInput> SavedCategoryTranslations { get; } = [];

        public Task<CategoryDto[]> GetCategoriesAsync() => Task.FromResult(Categories.ToArray());

        public Task<MenuItem[]> GetMenuItemsAsync() => Task.FromResult(MenuItems.ToArray());

        public Task<ApiResult> SaveCategoryAsync(int? id, CategoryInput input)
        {
            var category = new Category { Id = id ?? 123, Name = input.Name };
            Categories.Add(new CategoryDto(category.Id, input.Name, input.Description,
                input.SortOrder, input.IsVisible, 0));
            return Task.FromResult(ApiResult.Ok(category.Id));
        }

        public Task<ApiResult> DeleteCategoryAsync(int id) => Task.FromResult(ApiResult.Ok());

        public Task<ApiResult> SaveMenuItemAsync(int? id, MenuItemInput input)
        {
            var item = new MenuItem { Id = id ?? 321, Name = input.Name, ImageUrl = input.ImageUrl };
            MenuItems.Add(item);
            return Task.FromResult(ApiResult.Ok(item.Id));
        }

        public Task<ApiResult> DeleteMenuItemAsync(int id) => Task.FromResult(ApiResult.Ok());

        public Task<UserOutputDto[]> GetUsersAsync() => Task.FromResult(Array.Empty<UserOutputDto>());

        public Task<ApiResult> CreateUserAsync(UserInput input) => Task.FromResult(ApiResult.Ok());

        public Task<ApiResult> UpdateUserAsync(int id, UserUpdateInput input) => Task.FromResult(ApiResult.Ok());

        public Task<ApiResult> SetUserPasswordAsync(int id, string password) => Task.FromResult(ApiResult.Ok());

        public Task<PublicSettingsDto?> GetPublicSettingsAsync() =>
            Task.FromResult<PublicSettingsDto?>(new PublicSettingsDto(
                "Test", "USD", "light", "en", "en,fa,tr,ar"));

        public Task<RestaurantSettings?> GetSettingsAsync() => Task.FromResult<RestaurantSettings?>(null);

        public Task<ApiResult> SaveSettingsAsync(SettingsInput input) => Task.FromResult(ApiResult.Ok());

        public Task<UploadResult?> UploadImageAsync(byte[] data, string contentType, string fileName) =>
            Task.FromResult<UploadResult?>(new UploadResult($"/images/{Guid.NewGuid()}"));

        public Task<CategoryTranslation[]> GetCategoryTranslationsAsync(int categoryId) =>
            Task.FromResult(CategoryTranslations.Where(t => t.CategoryId == categoryId).ToArray());

        public Task<ApiResult> SaveCategoryTranslationAsync(int categoryId, CategoryTranslationInput input)
        {
            SavedCategoryTranslations.Add(input);
            CategoryTranslations.RemoveAll(t => t.CategoryId == categoryId && t.Culture == input.Culture);
            CategoryTranslations.Add(new CategoryTranslation
            {
                CategoryId = categoryId,
                Culture = input.Culture,
                Name = input.Name,
                Description = input.Description,
            });
            return Task.FromResult(ApiResult.Ok());
        }

        public List<MenuItemTranslationInput> SavedMenuItemTranslations { get; } = [];

        public Task<MenuItemTranslation[]> GetMenuItemTranslationsAsync(int menuItemId) =>
            Task.FromResult(MenuItemTranslations.Where(t => t.MenuItemId == menuItemId).ToArray());

        public Task<ApiResult> SaveMenuItemTranslationAsync(int menuItemId, MenuItemTranslationInput input)
        {
            SavedMenuItemTranslations.Add(input);
            MenuItemTranslations.RemoveAll(t => t.MenuItemId == menuItemId && t.Culture == input.Culture);
            MenuItemTranslations.Add(new MenuItemTranslation
            {
                MenuItemId = menuItemId,
                Culture = input.Culture,
                Name = input.Name,
                Description = input.Description,
                Price = input.Price,
                Currency = input.Currency,
            });
            return Task.FromResult(ApiResult.Ok());
        }

        public Task<MenuItemImage[]> GetMenuItemImagesAsync(int menuItemId) =>
            Task.FromResult(MenuItemImages.Where(i => i.MenuItemId == menuItemId).ToArray());

        public Task<ApiResult> AddMenuItemImageAsync(int menuItemId, string url)
        {
            MenuItemImages.Add(new MenuItemImage
            {
                Id = MenuItemImages.Count + 1,
                MenuItemId = menuItemId,
                Url = url,
                SortOrder = MenuItemImages.Count,
            });
            return Task.FromResult(ApiResult.Ok());
        }

        public Task<ApiResult> DeleteMenuItemImageAsync(int imageId)
        {
            MenuItemImages.RemoveAll(i => i.Id == imageId);
            return Task.FromResult(ApiResult.Ok());
        }

        public List<(int MenuItemId, string ChosenUrl, string? FormerCoverUrl)> SetCoverCalls { get; } = [];

        public Task<ApiResult> SetMenuItemImageCoverAsync(int menuItemId, string url)
        {
            var item = MenuItems.FirstOrDefault(m => m.Id == menuItemId);
            var former = item?.ImageUrl;
            SetCoverCalls.Add((menuItemId, url, former));
            if (item is not null)
            {
                item.ImageUrl = url;
                var demoted = MenuItemImages.FirstOrDefault(i => i.MenuItemId == menuItemId && i.Url == former);
                if (demoted is not null && MenuItemImages.All(i => i.Url != former || i.SortOrder != 0))
                {
                    demoted.SortOrder = MenuItemImages.Where(i => i.Url != former).Select(i => i.SortOrder).DefaultIfEmpty(0).Min() - 1;
                }
            }

            return Task.FromResult(ApiResult.Ok());
        }

        public Task<MenuItemVideo?> GetMenuItemVideoAsync(int menuItemId) =>
            Task.FromResult<MenuItemVideo?>(null);

        public Task<ApiResult> SetMenuItemVideoAsync(int menuItemId, MenuItemVideoInput? input) =>
            Task.FromResult(ApiResult.Ok());

        public Task<UploadResult?> UploadVideoAsync(byte[] data, string contentType, string fileName) =>
            Task.FromResult<UploadResult?>(new UploadResult($"/media/videos/{Guid.NewGuid()}.mp4"));
    }
}

/// <summary>
/// Categories previously had no translation UI. Regression: CategoryEdit must
/// offer per-culture tabs for saved categories (like MenuItemEdit) and must
/// persist a Farsi translation through the admin API.
/// </summary>
public class CategoryEditTests : AdminComponentTest
{
    [Fact]
    public void NewCategory_ShowsAfterSaveHint_NotTranslationTabs()
    {
        var cut = RenderComponent<CategoryEdit>();

        Assert.Contains("Translations can be added after saving", cut.Markup);
        Assert.Empty(cut.FindAll("[role='tab']"));
    }

    [Fact]
    public void SavedCategory_ShowsTranslationTabs_ForEnabledCultures()
    {
        Api.Categories.Add(new CategoryDto(7, "Starters", "Small bites", 1, true, 0));

        var cut = RenderComponent<CategoryEdit>(p => p.Add(c => c.Id, 7));
        cut.WaitForState(() => cut.FindAll("[role='tab']").Count > 0);

        var tabs = cut.FindAll("[role='tab']").Select(t => t.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "English", "فارسی", "Türkçe", "العربية" }, tabs);
    }

    [Fact]
    public async Task SavedCategory_FaName_SavePersistsThroughApi()
    {
        Api.Categories.Add(new CategoryDto(7, "Starters", null, 1, true, 0));

        var cut = RenderComponent<CategoryEdit>(p => p.Add(c => c.Id, 7));
        cut.WaitForState(() => cut.FindAll("[role='tab']").Count > 0);

        // Open the Farsi tab and wait for its fields.
        cut.FindAll("[role='tab']").Single(t => t.TextContent.Contains("فارسی")).Click();
        cut.WaitForState(() => cut.FindComponents<InputText>().Count >= 2);

        // The second InputText is the translation name (the first is the
        // invariant category name above the tabs).
        var faName = cut.FindComponents<InputText>()[1];
        await cut.InvokeAsync(() => faName.Instance.ValueChanged.InvokeAsync("غذای آزمون"));
        cut.Find("[data-save-translation]").Click();

        var saved = Assert.Single(Api.SavedCategoryTranslations);
        Assert.Equal("fa", saved.Culture);
        Assert.Equal("غذای آزمون", saved.Name);
    }
}

/// <summary>
/// The gallery editor previously had no way to change which photo is the cover.
/// Regression: every non-cover photo gets a Set cover action, and using it
/// swaps the primary image through the admin API.
/// </summary>
public class MenuItemEditSetCoverTests : AdminComponentTest
{
    [Fact]
    public void Gallery_Photos_GetSetCoverButton()
    {
        Api.MenuItems.Add(new MenuItem { Id = 55, Name = "Kebab", ImageUrl = "/images/cover.png" });
        Api.MenuItemImages.Add(new MenuItemImage { Id = 1, MenuItemId = 55, Url = "/images/b.png", SortOrder = 1 });
        Api.MenuItemImages.Add(new MenuItemImage { Id = 2, MenuItemId = 55, Url = "/images/c.png", SortOrder = 2 });

        var cut = RenderComponent<MenuItemEdit>(p => p.Add(c => c.Id, 55));
        cut.WaitForState(() => cut.FindAll("[data-photo]").Count == 3);            // The current cover shows a badge instead of a button; the other two
            // photos each get a Set cover action.
            Assert.Single(cut.FindAll("[data-cover]"));
            Assert.Equal(2, cut.FindAll("[data-set-cover]").Count);
    }

    [Fact]
    public async Task SetCover_CallsApiWithChosenPhoto()
    {
        Api.MenuItems.Add(new MenuItem { Id = 55, Name = "Kebab", ImageUrl = "/images/cover.png" });
        Api.MenuItemImages.Add(new MenuItemImage { Id = 1, MenuItemId = 55, Url = "/images/b.png", SortOrder = 1 });

        var cut = RenderComponent<MenuItemEdit>(p => p.Add(c => c.Id, 55));
        cut.WaitForState(() => cut.FindAll("[data-photo]").Count == 2);

        await cut.InvokeAsync(() => cut.Find("[data-set-cover]").Click());

        // The editor reloads the gallery afterwards; the API recorded the swap
        // (old cover in, chosen photo out).
        var swap = Assert.Single(Api.SetCoverCalls);
        Assert.Equal(55, swap.MenuItemId);
        Assert.Equal("/images/b.png", swap.ChosenUrl);
        Assert.Equal("/images/cover.png", swap.FormerCoverUrl);
    }

    [Fact]
    public void TranslationTab_HasCurrencySelect_AndSavePassesItThrough()
    {
        Api.MenuItems.Add(new MenuItem { Id = 77, Name = "Tea" });

        var cut = RenderComponent<MenuItemEdit>(p => p.Add(c => c.Id, 77));
        cut.WaitForState(() => cut.FindAll("[role='tab']").Count > 0);

        // The translation section offers a currency picker next to the price:
        // the restaurant currency as the default (empty value), the rest as codes.
        var currencySelect = cut.Find("[data-translation-currency]");
        var codes = currencySelect.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();
        Assert.Contains("TOMAN", codes);
        Assert.DoesNotContain("USD", codes); // it is the default option instead
        var defaultOption = currencySelect.QuerySelectorAll("option").Single(o => o.GetAttribute("value") == "");
        Assert.Contains("Default (USD)", defaultOption.TextContent);

        cut.FindAll("[role='tab']").Single(t => t.TextContent.Contains("فارسی")).Click();
        cut.WaitForState(() => cut.Find("[data-translation-currency]").GetAttribute("value") != null);

        cut.Find("[data-translation-currency]").Change("TOMAN");
        var priceInput = cut.FindComponents<InputNumber<decimal?>>().Single();
        cut.InvokeAsync(() => priceInput.Instance.ValueChanged.InvokeAsync(99000m));
        cut.Find("[data-save-translation]").Click();

        var saved = Assert.Single(Api.SavedMenuItemTranslations);
        Assert.Equal("fa", saved.Culture);
        Assert.Equal(99000m, saved.Price);
        Assert.Equal("TOMAN", saved.Currency);
    }
}
