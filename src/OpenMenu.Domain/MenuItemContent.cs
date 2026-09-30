using System.ComponentModel.DataAnnotations;

namespace OpenMenu.Domain;

/// <summary>
/// Translated content of a menu item in one culture. The invariant (default)
/// values stay on MenuItem; translations overlay them when available.
/// </summary>
public class MenuItemTranslation
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }

    public MenuItem? MenuItem { get; set; }

    /// <summary>Neutral culture name, e.g. "en", "fa". Matches the app's supported cultures.</summary>
    [StringLength(10)]
    public string Culture { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Null keeps the invariant MenuItem.Price; set to override per culture. Precision via fluent config (12,2).</summary>
    public decimal? Price { get; set; }
}

/// <summary>
/// Translated name/description of a category in one culture. The invariant
/// values stay on Category.
/// </summary>
public class CategoryTranslation
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>Neutral culture name, e.g. "en", "fa". Matches the app's supported cultures.</summary>
    public string Culture { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }
}

/// <summary>
/// Additional image of a menu item beyond the primary ImageUrl. Order defines
/// gallery position (0 first); the primary ImageUrl is always prepended for display.
/// </summary>
public class MenuItemImage
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }

    public MenuItem? MenuItem { get; set; }

    /// <summary>Public URL of the image (usually /images/{guid}).</summary>
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

/// <summary>Video of a menu item. One video per item; null ContentType means an external URL.</summary>
public class MenuItemVideo
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }

    public MenuItem? MenuItem { get; set; }

    /// <summary>Public URL of the video file (/videos/{guid}) or an external link.</summary>
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;

    /// <summary>MIME type for serving; null for external URLs (the browser infers it).</summary>
    [StringLength(100)]
    public string? ContentType { get; set; }
}
