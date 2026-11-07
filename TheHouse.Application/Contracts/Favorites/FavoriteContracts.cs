namespace TheHouse.Application.Contracts.Favorites;

// NV-15 §6.1: elemento del listado de favoritos (datos públicos de NV-07).
public sealed record FavoriteItemResponse(
    string PropertyId,
    string Title,
    decimal Price,
    string Currency,
    string City,
    string? UniversityId,
    int Bedrooms,
    int Bathrooms,
    double AreaM2,
    DateTimeOffset FavoritedAt);

public sealed record FavoriteListResponse(
    IReadOnlyList<FavoriteItemResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record FavoriteIdsResponse(IReadOnlyList<string> PropertyIds);
