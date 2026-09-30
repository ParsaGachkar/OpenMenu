namespace OpenMenu.Application.Shared;

using OpenMenu.Domain;

/// <summary>Outcome of a write operation surfaced to the admin UI.</summary>
public sealed record ApiResult(bool Success, string? Error = null, int? CreatedId = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Ok(int createdId) => new(true, null, createdId);
    public static ApiResult Fail(string error) => new(false, error);
}

/// <summary>URL of an uploaded image, relative to the site root.</summary>
public sealed record UploadResult(string Url);

/// <summary>Public settings safe for non-admin users (same data the menu shows).</summary>
public sealed record PublicSettingsDto(
    string Name, string Currency, string Theme,
    string DefaultCulture, string EnabledCultures);

// Write models shared by the JSON endpoints (WebAssembly path) and the
// server-circuit path of the interactive-auto admin pages.

public sealed record CategoryInput(string Name, string? Description, int SortOrder, bool IsVisible);

public sealed record MenuItemInput(
    int CategoryId, string Name, string? Description, decimal Price,
    string? ImageUrl, int SortOrder, bool IsAvailable);

public sealed record UserInput(string Username, string Password, UserRole Role);

public sealed record UserUpdateInput(UserRole Role, bool IsActive);

public sealed record PasswordInput(string Password);

public sealed record SettingsInput(
    string Name, string? Description, string? LogoUrl,
    string Currency, string Theme, string? PrimaryColor,
    string DefaultCulture, string EnabledCultures);

// Translation and media payloads (feature: per-culture content, multi-image, video).

/// <summary>Overlay content for one culture. Null fields keep the invariant values.</summary>
public sealed record MenuItemTranslationInput(string Culture, string? Name, string? Description, decimal? Price);

/// <summary>Overlay content for one culture. Null fields keep the invariant values.</summary>
public sealed record CategoryTranslationInput(string Culture, string? Name, string? Description);

/// <summary>An additional gallery image of a menu item.</summary>
public sealed record MenuItemImageInput(string Url, int SortOrder);

/// <summary>The video of a menu item. ContentType is null for external URLs.</summary>
public sealed record MenuItemVideoInput(string Url, string? ContentType);
