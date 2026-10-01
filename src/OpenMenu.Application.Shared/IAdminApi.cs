namespace OpenMenu.Application.Shared;

using OpenMenu.Domain;

/// <summary>
/// Admin operations for the interactive-auto admin pages. Those pages first
/// render on a server circuit, then on WebAssembly, so both environments need
/// an implementation of this interface: AdminApiServer (OpenMenu.Infrastructure)
/// talks to the database directly, AdminApiClient (OpenMenu.Web.Client) calls
/// the JSON admin APIs over HttpClient. Docs:
/// https://learn.microsoft.com/aspnet/core/blazor/call-web-api
/// </summary>
public interface IAdminApi
{
    Task<CategoryDto[]> GetCategoriesAsync();
    Task<MenuItem[]> GetMenuItemsAsync();
    Task<ApiResult> SaveCategoryAsync(int? id, CategoryInput input); // null id = create
    Task<ApiResult> DeleteCategoryAsync(int id);
    Task<ApiResult> SaveMenuItemAsync(int? id, MenuItemInput input); // null id = create
    Task<ApiResult> DeleteMenuItemAsync(int id);
    Task<UserOutputDto[]> GetUsersAsync();
    Task<ApiResult> CreateUserAsync(UserInput input);
    Task<ApiResult> UpdateUserAsync(int id, UserUpdateInput input);
    Task<ApiResult> SetUserPasswordAsync(int id, string password);
    Task<PublicSettingsDto?> GetPublicSettingsAsync();
    Task<RestaurantSettings?> GetSettingsAsync();
    Task<ApiResult> SaveSettingsAsync(SettingsInput input);
    Task<UploadResult?> UploadImageAsync(byte[] data, string contentType, string fileName);

    // Per-culture content (features: default culture, enabled cultures, localized menu).
    Task<CategoryTranslation[]> GetCategoryTranslationsAsync(int categoryId);
    Task<ApiResult> SaveCategoryTranslationAsync(int categoryId, CategoryTranslationInput input);
    Task<MenuItemTranslation[]> GetMenuItemTranslationsAsync(int menuItemId);
    Task<ApiResult> SaveMenuItemTranslationAsync(int menuItemId, MenuItemTranslationInput input);

    // Multi-image gallery and video support.
    Task<MenuItemImage[]> GetMenuItemImagesAsync(int menuItemId);
    Task<ApiResult> AddMenuItemImageAsync(int menuItemId, string url);
    Task<ApiResult> DeleteMenuItemImageAsync(int imageId);

    /// <summary>
    /// Makes the gallery image with the given URL the item's cover: the chosen
    /// URL becomes the primary ImageUrl, the former cover is demoted into the
    /// gallery at the front. No-op when the URL is already the cover.
    /// </summary>
    Task<ApiResult> SetMenuItemImageCoverAsync(int menuItemId, string url);
    Task<MenuItemVideo?> GetMenuItemVideoAsync(int menuItemId);
    Task<ApiResult> SetMenuItemVideoAsync(int menuItemId, MenuItemVideoInput? input); // null input = remove

    // Video upload (separate from image upload: different size limit + MIME checks).
    Task<UploadResult?> UploadVideoAsync(byte[] data, string contentType, string fileName);
}
