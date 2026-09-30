namespace OpenMenu.Domain;

public class MenuItem
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Price as decimal(12,2). The currency comes from RestaurantSettings.</summary>
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Additional images (gallery). The primary ImageUrl is always shown first.</summary>
    public List<MenuItemImage> Images { get; set; } = [];

    /// <summary>Optional video of the dish (uploaded file or external URL).</summary>
    public MenuItemVideo? Video { get; set; }

    /// <summary>Per-culture translations; the invariant values above are the fallback.</summary>
    public List<MenuItemTranslation> Translations { get; set; } = [];

    public int SortOrder { get; set; }

    public bool IsAvailable { get; set; } = true;
}
