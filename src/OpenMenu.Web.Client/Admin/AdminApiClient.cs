using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenMenu.Domain;
using OpenMenu.Application.Shared;

namespace OpenMenu.Web.Client.Admin;

/// <summary>
/// IAdminApi implementation for the WebAssembly render path of the
/// interactive-auto admin pages: calls the server's JSON admin APIs with the
/// preconfigured HttpClient (the auth cookie flows automatically on
/// same-origin requests). The server-circuit render path uses AdminApiServer.
/// Docs: https://learn.microsoft.com/aspnet/core/blazor/call-web-api
/// </summary>
public sealed class AdminApiClient(HttpClient http) : IAdminApi
{
    public async Task<CategoryDto[]> GetCategoriesAsync() =>
        await http.GetFromJsonAsync<CategoryDto[]>("api/admin/categories") ?? [];

    public async Task<MenuItem[]> GetMenuItemsAsync() =>
        await http.GetFromJsonAsync<MenuItem[]>("api/admin/menu-items") ?? [];

    public Task<ApiResult> SaveCategoryAsync(int? id, CategoryInput input) =>
        id is null
            ? WriteAsync(() => http.PostAsJsonAsync("api/admin/categories", input))
            : WriteAsync(() => http.PutAsJsonAsync($"api/admin/categories/{id}", input));

    public Task<ApiResult> DeleteCategoryAsync(int id) =>
        DeleteAsync($"api/admin/categories/{id}/delete");

    public Task<ApiResult> SaveMenuItemAsync(int? id, MenuItemInput input) =>
        id is null
            ? WriteAsync(() => http.PostAsJsonAsync("api/admin/menu-items", input))
            : WriteAsync(() => http.PutAsJsonAsync($"api/admin/menu-items/{id}", input));

    public Task<ApiResult> DeleteMenuItemAsync(int id) => DeleteAsync($"api/admin/menu-items/{id}/delete");

    public async Task<UserOutputDto[]> GetUsersAsync() =>
        await http.GetFromJsonAsync<UserOutputDto[]>("api/admin/users") ?? [];

    public async Task<ApiResult> CreateUserAsync(UserInput input) =>
        await WriteAsync(() => http.PostAsJsonAsync("api/admin/users", input));

    public Task<ApiResult> UpdateUserAsync(int id, UserUpdateInput input) =>
        WriteAsync(() => http.PutAsJsonAsync($"api/admin/users/{id}", input));

    public async Task<ApiResult> SetUserPasswordAsync(int id, string password) =>
        await WriteAsync(() => http.PostAsJsonAsync($"api/admin/users/{id}/password", new PasswordInput(password)));

    public async Task<PublicSettingsDto?> GetPublicSettingsAsync() =>
        await http.GetFromJsonAsync<PublicSettingsDto>("api/public/settings");

    public async Task<RestaurantSettings?> GetSettingsAsync() =>
        await http.GetFromJsonAsync<RestaurantSettings>("api/admin/settings");

    public async Task<ApiResult> SaveSettingsAsync(SettingsInput input) =>
        await WriteAsync(() => http.PutAsJsonAsync("api/admin/settings", input));

    public async Task<UploadResult?> UploadImageAsync(byte[] data, string contentType, string fileName) =>
        await UploadFileAsync("api/admin/images", data, contentType, fileName);

    public async Task<UploadResult?> UploadVideoAsync(byte[] data, string contentType, string fileName) =>
        await UploadFileAsync("api/admin/videos", data, contentType, fileName);

    private async Task<UploadResult?> UploadFileAsync(string url, byte[] data, string contentType, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        using var response = await http.PostAsync(url, content);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UploadResult>()
            : null;
    }

    // ---- Per-culture content (features 1-3) ----

    public async Task<CategoryTranslation[]> GetCategoryTranslationsAsync(int categoryId) =>
        await http.GetFromJsonAsync<CategoryTranslation[]>($"api/admin/categories/{categoryId}/translations") ?? [];

    public Task<ApiResult> SaveCategoryTranslationAsync(int categoryId, CategoryTranslationInput input) =>
        WriteAsync(() => http.PutAsJsonAsync($"api/admin/categories/{categoryId}/translations", input));

    public async Task<MenuItemTranslation[]> GetMenuItemTranslationsAsync(int menuItemId) =>
        await http.GetFromJsonAsync<MenuItemTranslation[]>($"api/admin/menu-items/{menuItemId}/translations") ?? [];

    public Task<ApiResult> SaveMenuItemTranslationAsync(int menuItemId, MenuItemTranslationInput input) =>
        WriteAsync(() => http.PutAsJsonAsync($"api/admin/menu-items/{menuItemId}/translations", input));

    // ---- Multi-image gallery and video (features 4-5) ----

    public async Task<MenuItemImage[]> GetMenuItemImagesAsync(int menuItemId) =>
        await http.GetFromJsonAsync<MenuItemImage[]>($"api/admin/menu-items/{menuItemId}/images") ?? [];

    public Task<ApiResult> AddMenuItemImageAsync(int menuItemId, string url) =>
        WriteAsync(() => http.PostAsJsonAsync($"api/admin/menu-items/{menuItemId}/images", new MenuItemImageInput(url, 0)));

    public Task<ApiResult> DeleteMenuItemImageAsync(int imageId) =>
        DeleteAsync($"api/admin/menu-items/images/{imageId}/delete");

    public Task<ApiResult> SetMenuItemImageCoverAsync(int menuItemId, string url) =>
        WriteAsync(() => http.PutAsync($"api/admin/menu-items/images/cover?id={menuItemId}&url={Uri.EscapeDataString(url)}", content: null));

    public async Task<MenuItemVideo?> GetMenuItemVideoAsync(int menuItemId)
    {
        using var response = await http.GetAsync($"api/admin/menu-items/{menuItemId}/video");
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is 0)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<MenuItemVideo?>();
    }
    public Task<ApiResult> SetMenuItemVideoAsync(int menuItemId, MenuItemVideoInput? input) =>
        WriteAsync(() => http.PutAsJsonAsync($"api/admin/menu-items/{menuItemId}/video", input));

    private static async Task<ApiResult> WriteAsync(Func<Task<HttpResponseMessage>> send)
    {
        using var response = await send();
        if (response.IsSuccessStatusCode)
        {
            return ApiResult.Ok();
        }

        var error = await response.Content.TryReadErrorAsync();
        return ApiResult.Fail(error ?? "The request failed. Check the values and try again.");
    }

    // Deletes are POST (not GET) so prefetchers cannot trigger them.
    private Task<ApiResult> DeleteAsync(string url) =>
        WriteAsync(() => http.PostAsync(url, content: null));
}

public static class HttpContentExtensions
{
    /// <summary>Reads the endpoints' { error = "..." } body when present.</summary>
    public static async Task<string?> TryReadErrorAsync(this HttpContent content)
    {
        try
        {
            var body = await content.ReadFromJsonAsync<ErrorBody>();
            return body?.Error;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ErrorBody(string Error);
}
