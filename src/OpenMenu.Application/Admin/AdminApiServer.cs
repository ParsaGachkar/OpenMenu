using Microsoft.EntityFrameworkCore;
using OpenMenu.Domain;
using OpenMenu.Application.Shared;
using OpenMenu.Infrastructure.Auth;
using OpenMenu.Infrastructure.Data;


namespace OpenMenu.Application.Admin;

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
        return ApiResult.Ok(category.Id);
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
        return ApiResult.Ok(item.Id);
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
        return settings is null
            ? null
            : new PublicSettingsDto(settings.Name, settings.Currency, settings.Theme,
                settings.DefaultCulture, settings.EnabledCultures);
    }

    public Task<RestaurantSettings?> GetSettingsAsync() =>
        db.RestaurantSettings.FirstOrDefaultAsync();

    public async Task<ApiResult> SaveSettingsAsync(SettingsInput input)
    {
        // The default culture must be one of the enabled ones; both must be valid.
        var enabled = SupportedCultures.SplitEnabled(input.EnabledCultures, input.DefaultCulture);
        var defaultCulture = SupportedCultures.IsValid(input.DefaultCulture)
            && enabled.Contains(SupportedCultures.Normalize(input.DefaultCulture))
                ? SupportedCultures.Normalize(input.DefaultCulture)
                : enabled[0];

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
        settings.DefaultCulture = defaultCulture;
        settings.EnabledCultures = string.Join(",", enabled);
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

        // Id is ValueGeneratedNever: the app supplies the random GUID.
        var image = new ImageFile { Id = Guid.NewGuid(), ContentType = contentType, Data = data };
        db.ImageFiles.Add(image);
        await db.SaveChangesAsync();
        return new UploadResult($"/images/{image.Id}");
    }

    // ---- Per-culture content (features 1-3) ----

    public async Task<CategoryTranslation[]> GetCategoryTranslationsAsync(int categoryId) =>
        await db.CategoryTranslations
            .Where(t => t.CategoryId == categoryId)
            .AsNoTracking().ToArrayAsync();

    public async Task<ApiResult> SaveCategoryTranslationAsync(int categoryId, CategoryTranslationInput input)
    {
        if (!SupportedCultures.IsValid(input.Culture))
        {
            return ApiResult.Fail("Unsupported culture.");
        }

        if (!await db.Categories.AnyAsync(c => c.Id == categoryId))
        {
            return ApiResult.Fail("Category not found.");
        }

        var culture = SupportedCultures.Normalize(input.Culture);
        var translation = await db.CategoryTranslations
            .FirstOrDefaultAsync(t => t.CategoryId == categoryId && t.Culture == culture);

        if (translation is null)
        {
            translation = new CategoryTranslation { CategoryId = categoryId, Culture = culture };
            db.CategoryTranslations.Add(translation);
        }

        translation.Name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim();
        translation.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();

        // Drop the row entirely when it no longer overrides anything.
        if (translation.Name is null && translation.Description is null)
        {
            db.CategoryTranslations.Remove(translation);
        }

        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<MenuItemTranslation[]> GetMenuItemTranslationsAsync(int menuItemId) =>
        await db.MenuItemTranslations
            .Where(t => t.MenuItemId == menuItemId)
            .AsNoTracking().ToArrayAsync();

    public async Task<ApiResult> SaveMenuItemTranslationAsync(int menuItemId, MenuItemTranslationInput input)
    {
        if (!SupportedCultures.IsValid(input.Culture))
        {
            return ApiResult.Fail("Unsupported culture.");
        }

        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId))
        {
            return ApiResult.Fail("Menu item not found.");
        }

        var culture = SupportedCultures.Normalize(input.Culture);
        var translation = await db.MenuItemTranslations
            .FirstOrDefaultAsync(t => t.MenuItemId == menuItemId && t.Culture == culture);

        if (translation is null)
        {
            translation = new MenuItemTranslation { MenuItemId = menuItemId, Culture = culture };
            db.MenuItemTranslations.Add(translation);
        }

        translation.Name = string.IsNullOrWhiteSpace(input.Name) ? null : input.Name.Trim();
        translation.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        translation.Price = input.Price;

        // Drop the row entirely when it no longer overrides anything.
        if (translation.Name is null && translation.Description is null && translation.Price is null)
        {
            db.MenuItemTranslations.Remove(translation);
        }

        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    // ---- Multi-image gallery (feature 4) ----

    public async Task<MenuItemImage[]> GetMenuItemImagesAsync(int menuItemId) =>
        await db.MenuItemImages
            .Where(i => i.MenuItemId == menuItemId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .AsNoTracking().ToArrayAsync();

    public async Task<ApiResult> AddMenuItemImageAsync(int menuItemId, string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500)
        {
            return ApiResult.Fail("Invalid image URL.");
        }

        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId))
        {
            return ApiResult.Fail("Menu item not found.");
        }

        var nextOrder = await db.MenuItemImages
            .Where(i => i.MenuItemId == menuItemId)
            .OrderByDescending(i => i.SortOrder)
            .Select(i => i.SortOrder)
            .FirstOrDefaultAsync();

        db.MenuItemImages.Add(new MenuItemImage { MenuItemId = menuItemId, Url = url.Trim(), SortOrder = nextOrder + 1 });
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    public async Task<ApiResult> DeleteMenuItemImageAsync(int imageId)
    {
        var deleted = await db.MenuItemImages.Where(i => i.Id == imageId).ExecuteDeleteAsync();
        return deleted == 0 ? ApiResult.Fail("Image not found.") : ApiResult.Ok();
    }

    // ---- Video (feature 5) ----

    public async Task<MenuItemVideo?> GetMenuItemVideoAsync(int menuItemId) =>
        await db.MenuItemVideos.AsNoTracking().FirstOrDefaultAsync(v => v.MenuItemId == menuItemId);

    public async Task<ApiResult> SetMenuItemVideoAsync(int menuItemId, MenuItemVideoInput? input)
    {
        if (input is null)
        {
            await db.MenuItemVideos.Where(v => v.MenuItemId == menuItemId).ExecuteDeleteAsync();
            return ApiResult.Ok();
        }

        if (string.IsNullOrWhiteSpace(input.Url) || input.Url.Length > 500)
        {
            return ApiResult.Fail("Invalid video URL.");
        }

        if (!await db.MenuItems.AnyAsync(m => m.Id == menuItemId))
        {
            return ApiResult.Fail("Menu item not found.");
        }

        var video = await db.MenuItemVideos.FirstOrDefaultAsync(v => v.MenuItemId == menuItemId);
        if (video is null)
        {
            video = new MenuItemVideo { MenuItemId = menuItemId };
            db.MenuItemVideos.Add(video);
        }

        video.Url = input.Url.Trim();
        video.ContentType = string.IsNullOrWhiteSpace(input.ContentType) ? null : input.ContentType.Trim();
        await db.SaveChangesAsync();
        return ApiResult.Ok();
    }

    // Videos are large binary blobs: stored on the mounted media volume, never
    // in the database. The DB keeps only the MenuItemVideo metadata row.
    public async Task<UploadResult?> UploadVideoAsync(byte[] data, string contentType, string fileName)
    {
        const long maxFileSize = 50 * 1024 * 1024;
        if (data.Length == 0 || data.Length > maxFileSize || !contentType.StartsWith("video/"))
        {
            return null;
        }

        var mediaRoot = Path.Combine(AppContext.BaseDirectory, "media", "videos");
        Directory.CreateDirectory(mediaRoot);

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || extension.Length > 10)
        {
            extension = contentType switch
            {
                "video/webm" => ".webm",
                "video/ogg" => ".ogv",
                _ => ".mp4",
            };
        }

        var id = Guid.NewGuid();
        await File.WriteAllBytesAsync(Path.Combine(mediaRoot, id + extension), data);
        return new UploadResult($"/media/videos/{id}{extension}");
    }
}
