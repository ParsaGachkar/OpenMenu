namespace OpenMenu.Domain;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public List<MenuItem> Items { get; set; } = [];

    /// <summary>Per-culture translations; the invariant values above are the fallback.</summary>
    public List<CategoryTranslation> Translations { get; set; } = [];
}
