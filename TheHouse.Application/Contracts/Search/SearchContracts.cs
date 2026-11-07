namespace TheHouse.Application.Contracts.Search;

// NV-14 §6.1: respuestas públicas sin dirección, coordenadas reales ni dueño.
public sealed record ApproxLocation(double Lat, double Lng);

public sealed record RoomSearchItem(
    string Id,
    string BuildingId,
    string Title,
    int Price,
    string Currency,
    string? RoomType,
    string? Bathroom,
    string BuildingType,
    string AllowedTenants,
    IReadOnlyList<string> Services,
    double DistanceKm,
    int WalkingMinutes,
    ApproxLocation ApproxLocation,
    string? ThumbnailUrl,
    bool Demo);

public sealed record RoomSearchResponse(IReadOnlyList<RoomSearchItem> Items, int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record RoomMarker(string Id, int Price, ApproxLocation ApproxLocation, double DistanceKm);

public sealed record RoomMapResponse(IReadOnlyList<RoomMarker> Items);

public sealed record RoomCountResponse(int Count);

public sealed record SearchUniversity(string Id, string Code, string Name);

public sealed record SearchBuilding(string Id, string Name, string Type, string AllowedTenants, IReadOnlyList<string> Services, string? Rules);

public sealed record RoomDetailResponse(
    string Id,
    string BuildingId,
    string Title,
    int Price,
    string Currency,
    string? RoomType,
    string? Bathroom,
    decimal? SizeM2,
    IReadOnlyList<string> Furniture,
    string? AvailableFrom,
    string? MinContract,
    double DistanceKm,
    int WalkingMinutes,
    ApproxLocation ApproxLocation,
    SearchUniversity? NearestUniversity,
    SearchBuilding Building,
    string? ThumbnailUrl,
    bool Demo);

public sealed record PlaceItem(string Id, string Name, string Type, ApproxLocation ApproxLocation, double DistanceKm);

public sealed record PlaceSearchResponse(IReadOnlyList<PlaceItem> Items);
