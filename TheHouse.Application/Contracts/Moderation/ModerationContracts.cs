using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Landlord;

namespace TheHouse.Application.Contracts.Moderation;

// NV-21 §6: entradas estrictas.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DecisionRequest
{
    public string? Reason { get; init; }

    public long? Version { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReasonRequest
{
    public string? Reason { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateReportRequest
{
    public string? TargetType { get; init; }

    public string? TargetId { get; init; }

    public string? Reason { get; init; }

    public string? Details { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ResolveReportRequest
{
    public string? Action { get; init; }

    public string? Reason { get; init; }
}

public sealed record LastDecision(string Decision, string? Reason, DateTimeOffset DecidedAt);

public sealed record VerificationResponse(string Status, LastDecision? LastDecision);

public sealed record ReviewLandlord(string FullName, string RentalMode, string? Rfc);

public sealed record AdminBuildingDetailResponse(
    BuildingResponse Building,
    IReadOnlyList<RoomResponse> Rooms,
    ReviewLandlord? Landlord,
    LastDecision? LastDecision);

public sealed record AdminBuildingListResponse(IReadOnlyList<BuildingResponse> Items, int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record ReportCreatedResponse(string Id, string Status, DateTimeOffset CreatedAt);

public sealed record AdminReportResponse(
    string Id,
    string ReporterId,
    string TargetType,
    string TargetId,
    string Reason,
    string? Details,
    string Status,
    string? Resolution,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed record AdminReportListResponse(IReadOnlyList<AdminReportResponse> Items, int Page, int PageSize, long TotalItems, int TotalPages);

public sealed record AccountStatusResponse(string UserId, bool Suspended, string? Reason);

public sealed record AuditEventResponse(string Id, string ActorId, string Action, string TargetType, string TargetId, string? Reason, DateTimeOffset CreatedAt);

public sealed record AuditListResponse(IReadOnlyList<AuditEventResponse> Items, int Page, int PageSize, long TotalItems, int TotalPages);
