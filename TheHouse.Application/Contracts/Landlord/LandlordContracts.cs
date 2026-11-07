using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Landlord;

// NV-12 §6.1: entradas estrictas; `ownerId`, `status`, distancias y
// campos calculados no existen aquí (enviarlos → 400).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateLandlordProfileRequest
{
    public string? FullName { get; init; }

    public string? Phone { get; init; }

    public string? RentalMode { get; init; }

    public string? Rfc { get; init; }

    public string? AcceptedTermsVersion { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AddressRequest
{
    public string? Street { get; init; }

    public string? Number { get; init; }

    public string? Neighborhood { get; init; }

    public string? City { get; init; }

    public string? State { get; init; }

    public string? PostalCode { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LocationRequest
{
    public double? Lat { get; init; }

    public double? Lng { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class BuildingRequest
{
    public string? Type { get; init; }

    public string? Name { get; init; }

    public AddressRequest? Address { get; init; }

    public LocationRequest? Location { get; init; }

    public string? NearestUniversityId { get; init; }

    public string? AllowedTenants { get; init; }

    public IReadOnlyList<string>? Services { get; init; }

    public string? Rules { get; init; }

    // Solo en PATCH: versión que el cliente leyó.
    public long? Version { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RoomDataRequest
{
    public int? Price { get; init; }

    public decimal? SizeM2 { get; init; }

    public string? Type { get; init; }

    public string? Bathroom { get; init; }

    public IReadOnlyList<string>? Furniture { get; init; }

    public string? AvailableFrom { get; init; }

    public string? MinContract { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateRoomRequest
{
    public int? Number { get; init; }

    public int? Floor { get; init; }

    public int? Price { get; init; }

    public decimal? SizeM2 { get; init; }

    public string? Type { get; init; }

    public string? Bathroom { get; init; }

    public IReadOnlyList<string>? Furniture { get; init; }

    public string? AvailableFrom { get; init; }

    public string? MinContract { get; init; }

    public long? Version { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GenerateRoomsRequest
{
    public int? Floors { get; init; }

    public IReadOnlyList<int>? RoomsPerFloor { get; init; }

    public string? Numbering { get; init; }

    public RoomDataRequest? Base { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CopyRoomRequest
{
    public string? SourceRoomId { get; init; }

    public long? Version { get; init; }
}

public sealed record LandlordProfileResponse(
    string FullName,
    string Phone,
    string RentalMode,
    string? Rfc,
    string AcceptedTermsVersion,
    DateTimeOffset CreatedAt);

public sealed record AddressResponse(string Street, string Number, string Neighborhood, string City, string State, string PostalCode);

public sealed record LocationResponse(double Lat, double Lng);

public sealed record UniversityReference(string Id, string Code, string Name);

public sealed record BuildingResponse(
    string Id,
    string Type,
    string Name,
    AddressResponse Address,
    LocationResponse Location,
    UniversityReference? NearestUniversity,
    double DistanceToUniversityKm,
    int WalkingMinutes,
    string AllowedTenants,
    IReadOnlyList<string> Services,
    string? Rules,
    string Status,
    int RoomsTotal,
    int RoomsAvailable,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LandlordSummary(int Buildings, int Rooms, int Available, int OccupancyPercent, long MonthlyIncome);

public sealed record BuildingListResponse(
    IReadOnlyList<BuildingResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages,
    LandlordSummary Summary);

public sealed record RoomResponse(
    string Id,
    string BuildingId,
    int Number,
    int Floor,
    int? Price,
    string Currency,
    decimal? SizeM2,
    string? Type,
    string? Bathroom,
    IReadOnlyList<string> Furniture,
    string? AvailableFrom,
    string? MinContract,
    string Status,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RoomListResponse(IReadOnlyList<RoomResponse> Items, int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record GeneratedRoomsResponse(IReadOnlyList<RoomResponse> Items);

public sealed record PublicationStep(string Id, bool Done);

public sealed record PublicationResponse(bool CanPublish, IReadOnlyList<PublicationStep> Steps);
