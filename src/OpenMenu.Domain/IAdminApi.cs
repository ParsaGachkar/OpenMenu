namespace OpenMenu.Domain;

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
}
