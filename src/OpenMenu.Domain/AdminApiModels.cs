namespace OpenMenu.Domain;

/// <summary>Outcome of a write operation surfaced to the admin UI.</summary>
public sealed record ApiResult(bool Success, string? Error = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Fail(string error) => new(false, error);
}

/// <summary>URL of an uploaded image, relative to the site root.</summary>
public sealed record UploadResult(string Url);

/// <summary>Public settings safe for non-admin users (same data the menu shows).</summary>
public sealed record PublicSettingsDto(string Name, string Currency, string Theme);

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
    string Currency, string Theme, string? PrimaryColor);
