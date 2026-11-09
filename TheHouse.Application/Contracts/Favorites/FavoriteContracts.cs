using TheHouse.Application.Contracts.Search;

namespace TheHouse.Application.Contracts.Favorites;

// NV-15 1.2.0 §6.1: favorito de un cuarto publicado (datos públicos de NV-14).
// `PropertyId` es de transición: la propiedad de NV-07 de origen o null.
public sealed record FavoriteItemResponse(
    string RoomId,
    string BuildingId,
    string Title,
    int Price,
    string Currency,
    string? RoomType,
    string? Bathroom,
    string City,
    ApproxLocation ApproxLocation,
    bool Demo,
    DateTimeOffset FavoritedAt,
    string? PropertyId);

public sealed record FavoriteListResponse(
    IReadOnlyList<FavoriteItemResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record FavoriteIdsResponse(IReadOnlyList<string> RoomIds, IReadOnlyList<string> PropertyIds);
