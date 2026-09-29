namespace OpenMenu.Domain;

/// <summary>Single-row table: the one restaurant this installation serves.</summary>
public class RestaurantSettings
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "IRR", "USD".</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>UI theme key, e.g. "light", "dark", "cupcake". Used by the DaisyUI frontend.</summary>
    public string Theme { get; set; } = "light";

    /// <summary>Optional override for the theme's primary color (CSS color, e.g. "#7c3aed"). Null keeps the theme default.</summary>
    public string? PrimaryColor { get; set; }
}
