namespace OpenMenu.Domain;

/// <summary>
/// Flat projection of Category for API responses. Entities have circular
/// navigations (Category.Items ↔ MenuItem.Category) and must not be
/// serialized directly; EF Core translates ItemsCount into a subquery.
/// </summary>
public sealed record CategoryDto(
    int Id,
    string Name,
    string? Description,
    int SortOrder,
    bool IsVisible,
    int ItemsCount);
