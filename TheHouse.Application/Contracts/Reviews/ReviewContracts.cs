using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Reviews;

// NV-19 §6.1: reseñas. Las entradas rechazan campos desconocidos
// (asignación masiva): ni `authorId`, ni `status`, ni promedios del cliente.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LandlordScoresInput
{
    public int? Communication { get; init; }

    public int? Attention { get; init; }

    public int? Compliance { get; init; }

    public int? Respect { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PropertyScoresInput
{
    public int? Cleanliness { get; init; }

    public int? Location { get; init; }

    public int? Value { get; init; }

    public int? Condition { get; init; }

    public int? Services { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviewInput
{
    public LandlordScoresInput? Landlord { get; init; }

    public PropertyScoresInput? Property { get; init; }

    public string? Comment { get; init; }
}

public sealed record ReviewRoomSummary(string Id, string BuildingId, string Title, string City);

public sealed record ReviewAuthor(string Name);

public sealed record LandlordScoresResponse(int Communication, int Attention, int Compliance, int Respect);

public sealed record PropertyScoresResponse(int Cleanliness, int Location, int Value, int Condition, int Services);

public sealed record ReviewResponse(
    string Id,
    ReviewRoomSummary Room,
    ReviewAuthor Author,
    string Basis,
    LandlordScoresResponse Landlord,
    PropertyScoresResponse Property,
    double LandlordRating,
    double PropertyRating,
    string? Comment,
    bool Edited,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Mis reseñas: incluye el estado para saber si una se ocultó.
public sealed record MyReviewResponse(
    string Id,
    ReviewRoomSummary Room,
    ReviewAuthor Author,
    string Basis,
    LandlordScoresResponse Landlord,
    PropertyScoresResponse Property,
    double LandlordRating,
    double PropertyRating,
    string? Comment,
    bool Edited,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ReviewAverage(double? Average, int Count);

public sealed record CriteriaAverages(
    double? Communication,
    double? Attention,
    double? Compliance,
    double? Respect,
    double? Cleanliness,
    double? Location,
    double? Value,
    double? Condition,
    double? Services);

public sealed record ReviewSummary(ReviewAverage Property, ReviewAverage Landlord, CriteriaAverages Criteria);

public sealed record RoomReviewListResponse(
    ReviewSummary Summary,
    IReadOnlyList<ReviewResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record MyReviewListResponse(
    IReadOnlyList<MyReviewResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record ReviewEligibilityResponse(
    bool Eligible,
    string? Basis,
    string? Reason,
    string? ReviewId,
    DateTimeOffset? EditableUntil);

public sealed record ReviewStatusResponse(string Id, string Status);
