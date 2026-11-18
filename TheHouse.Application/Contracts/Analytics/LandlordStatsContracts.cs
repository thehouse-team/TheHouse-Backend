namespace TheHouse.Application.Contracts.Analytics;

// NV-25 §6.1: estadísticas del arrendador. Solo agregados; nunca quién vio,
// guardó o solicitó un cuarto.
public sealed record StatsTotals(
    int Views,
    int Favorites,
    int Requests,
    int AcceptedRequests,
    int Visits,
    int ConfirmedVisits,
    int Agreements,
    double? Conversion);

public sealed record StatsDay(string Date, int Views, int Requests);

public sealed record LandlordStatsResponse(
    string From,
    string To,
    string? BuildingId,
    StatsTotals Totals,
    StatsTotals Previous,
    IReadOnlyList<StatsDay> Daily,
    int FavoritesNow,
    DateTimeOffset UpdatedAt);

public sealed record StatsRating(double? Average, int Count);

public sealed record RoomStatsItem(
    string RoomId,
    string BuildingId,
    string BuildingName,
    int Number,
    string Status,
    int? Price,
    int Views,
    int Favorites,
    int Requests,
    int Visits,
    int Agreements,
    double? Conversion,
    StatsRating? Rating);

public sealed record RoomStatsListResponse(
    IReadOnlyList<RoomStatsItem> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record StatsRecommendation(
    string Code,
    string Priority,
    string BuildingId,
    string? RoomId,
    string Title,
    string Detail);

public sealed record StatsRecommendationsResponse(IReadOnlyList<StatsRecommendation> Items);
