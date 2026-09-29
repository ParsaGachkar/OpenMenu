using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Infrastructure.Auth;
using OpenMenu.Infrastructure.Data;

namespace OpenMenu.Infrastructure.Admin;

/// <summary>
/// IAdminApi implementation for the server-circuit render path of the
/// interactive-auto admin pages: talks to the database directly through
/// AppDbContext. The WebAssembly render path uses AdminApiClient instead,
/// which calls the JSON admin APIs (thin wrappers over this same interface).
/// Docs: https://learn.microsoft.com/aspnet/core/blazor/call-web-api
/// </summary>
public sealed class AdminApiServer(AppDbContext db) : IAdminApi
{
    public async Task<CategoryDto[]> GetCategoriesAsync() =>
        await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            // Flat projection: entities carry circular navigations that cannot
            // be serialized (the object-cycle exception).
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.SortOrder, c.IsVisible, c.Items.Count))
            .AsNoTracking().ToArrayAsync();

    public async Task<MenuItem[]> GetMenuItemsAsync() =>
        await db.MenuItems.OrderBy(m => m.CategoryId).ThenBy(m => m.SortOrder).ThenBy(m => m.Name)
            .AsNoTracking().ToArrayAsync();

    public async Task<ApiResult> SaveCategoryAsync(int? id, CategoryInput input)
    {
        Category? category;
        if (id is null)
        {
            category = new Category();
            db.Categories.Add(category);
        }
        else
        {
            category = await db.Categories.FindAsync(id);
            if (category is null)
            {
                return ApiResult.Fail("Category not found.");
            }
        }

        category.Name = input.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        category.SortOrder = input.SortOrder;
        category.IsVisible = input.IsVisible;
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<ApiResult> DeleteCategoryAsync(int id)
    {
        var deleted = await db.Categories.Where(c => c.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? ApiResult.Fail("Category not found.") : ApiResult.Ok();
    }

    public async Task<ApiResult> SaveMenuItemAsync(int? id, MenuItemInput input)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId))
        {
            return ApiResult.Fail("Unknown category.");
        }

        MenuItem? item;
        if (id is null)
        {
            item = new MenuItem();
            db.MenuItems.Add(item);
        }
        else
        {
            item = await db.MenuItems.FindAsync(id);
            if (item is null)
            {
                return ApiResult.Fail("Menu item not found.");
            }
        }

        item.CategoryId = input.CategoryId;
        item.Name = input.Name.Trim();
        item.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        item.Price = input.Price;
        item.ImageUrl = input.ImageUrl;
        item.SortOrder = input.SortOrder;
        item.IsAvailable = input.IsAvailable;
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<ApiResult> DeleteMenuItemAsync(int id)
    {
        var deleted = await db.MenuItems.Where(m => m.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? ApiResult.Fail("Menu item not found.") : ApiResult.Ok();
    }

    public async Task<UserOutputDto[]> GetUsersAsync() =>
        await db.Users.OrderBy(u => u.Username)
            .Select(u => new UserOutputDto(u.Id, u.Username, u.Role, u.IsActive))
            .AsNoTracking().ToArrayAsync();

    public async Task<ApiResult> CreateUserAsync(UserInput input)
    {
        var username = input.Username.Trim();
        if (username.Length is 0 or > 64 || input.Password.Length < 8)
        {
            return ApiResult.Fail("Invalid username or password too short (min 8).");
        }

        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            return ApiResult.Fail("Username already exists.");
        }

        db.Users.Add(new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(input.Password),
            Role = input.Role,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<ApiResult> UpdateUserAsync(int id, UserUpdateInput input)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return ApiResult.Fail("User not found.");
        }

        user.Role = input.Role;
        user.IsActive = input.IsActive;
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<ApiResult> SetUserPasswordAsync(int id, string password)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return ApiResult.Fail("User not found.");
        }

        if (password.Length < 8)
        {
            return ApiResult.Fail("Password too short (min 8).");
        }

        user.PasswordHash = PasswordHasher.Hash(password);
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<PublicSettingsDto?> GetPublicSettingsAsync()
    {
        var settings = await db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync();
        return settings is null ? null : new PublicSettingsDto(settings.Name, settings.Currency, settings.Theme);
    }

    public Task<RestaurantSettings?> GetSettingsAsync() =>
        db.RestaurantSettings.FirstOrDefaultAsync();

    public async Task<ApiResult> SaveSettingsAsync(SettingsInput input)
    {
        var settings = await db.RestaurantSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new RestaurantSettings { Id = 1 };
            db.RestaurantSettings.Add(settings);
        }

        settings.Name = input.Name.Trim();
        settings.Description = string.IsNullOrWhiteSpace(input.Description) ? string.Empty : input.Description.Trim();
        settings.LogoUrl = input.LogoUrl;
        settings.Currency = input.Currency.Trim().ToUpperInvariant();
        settings.Theme = input.Theme;
        settings.PrimaryColor = string.IsNullOrWhiteSpace(input.PrimaryColor) ? null : input.PrimaryColor.Trim();
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<UploadResult?> UploadImageAsync(byte[] data, string contentType, string fileName)
    {
        const long maxFileSize = 2 * 1024 * 1024;
        if (data.Length == 0 || data.Length > maxFileSize || !contentType.StartsWith("image/"))
        {
            return null;
        }

        var image = new ImageFile { ContentType = contentType, Data = data };
        db.ImageFiles.Add(image);
        await db.SaveChangesAsync();
        return new UploadResult($"/images/{image.Id}");
    }
}
