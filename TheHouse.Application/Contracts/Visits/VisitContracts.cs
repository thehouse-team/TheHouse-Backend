using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Visits;

// NV-17 §6.1: `scheduledAt` llega como texto para exigir el desplazamiento
// horario explícito (ISO 8601 con Z o ±hh:mm).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateVisitRequest
{
    public string? PropertyId { get; init; }

    public string? ScheduledAt { get; init; }

    public string? Message { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CancelVisitRequest
{
    public string? Reason { get; init; }
}

public sealed record VisitPropertySummary(string Id, string Title, string City);

public sealed record VisitResponse(
    string Id,
    VisitPropertySummary Property,
    DateTimeOffset ScheduledAt,
    string TimeZone,
    string Status,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt);

public sealed record VisitListResponse(
    IReadOnlyList<VisitResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

// Resultado de crear: `Replayed` indica que se devolvió la visita original
// de la misma Idempotency-Key.
public sealed record CreateVisitResult(VisitResponse Visit, bool Replayed);
