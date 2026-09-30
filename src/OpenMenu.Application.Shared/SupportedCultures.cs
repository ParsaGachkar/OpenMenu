namespace OpenMenu.Application.Shared;

/// <summary>
/// The cultures this installation ships content for: UI resx translations and
/// per-culture menu content. EnabledCultures/DefaultCulture on RestaurantSettings
/// pick a subset of these; anything else is rejected on save and on /culture/set.
/// </summary>
public static class SupportedCultures
{
    public static readonly string[] All = ["en", "fa", "tr", "ar"];

    public static bool IsValid(string culture) =>
        All.Contains(culture.Trim().TrimEnd().ToLowerInvariant());

    /// <summary>
    /// Parses the comma-separated EnabledCultures setting into a normalized list,
    /// falling back to the default culture when nothing valid remains.
    /// </summary>
    public static string[] SplitEnabled(string? enabledCultures, string defaultCulture = "en")
    {
        var list = (enabledCultures ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToLowerInvariant())
            .Where(IsValid)
            .Distinct()
            .ToArray();

        return list.Length == 0 ? [Normalize(defaultCulture)] : list;
    }

    public static string Normalize(string culture) => culture.Trim().ToLowerInvariant();
}
