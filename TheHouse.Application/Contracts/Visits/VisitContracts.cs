using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Visits;

// NV-17 §6.1: `scheduledAt` llega como texto para exigir el desplazamiento
// horario explícito (ISO 8601 con Z o ±hh:mm).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateVisitRequest
{
    public string? RoomId { get; init; }

    // Transición (NV-17 1.2.0): id de propiedad de NV-07; exactamente uno de los dos.
    public string? PropertyId { get; init; }

    public string? ScheduledAt { get; init; }

    public string? Message { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CancelVisitRequest
{
    public string? Reason { get; init; }
}

public sealed record VisitRoomSummary(string Id, string BuildingId, string Title, string City);

// Transición (NV-17 1.2.0): propiedad de NV-07 de la que salió el cuarto.
public sealed record VisitPropertySummary(string Id, string Title, string City);

// NV-17 1.3.0: campos agregados sin quitar ninguno. `Address` y `Location`
// solo con la visita `confirmed` (RNF-02).
public sealed record VisitResponse(
    string Id,
    VisitRoomSummary Room,
    VisitPropertySummary? Property,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? PreviousScheduledAt,
    string TimeZone,
    string Status,
    string? Message,
    VisitLandlordSummary Landlord,
    VisitAddress? Address,
    VisitLocation? Location,
    VisitCancellation? Cancellation,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt);

public sealed record VisitLandlordSummary(string Name);

public sealed record VisitAddress(string Street, string Number, string Neighborhood, string City, string State, string PostalCode);

public sealed record VisitLocation(double Lat, double Lng);

public sealed record VisitCancellation(string By, string? Reason, string? Note);

// Vista del arrendador: sin dirección (ya la conoce) ni datos de contacto.
public sealed record VisitStudentSummary(string? Name, string? University);

public sealed record LandlordVisitResponse(
    string Id,
    VisitRoomSummary Room,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? PreviousScheduledAt,
    string TimeZone,
    string Status,
    string? Message,
    VisitStudentSummary Student,
    VisitCancellation? Cancellation,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt);

public sealed record LandlordVisitListResponse(
    IReadOnlyList<LandlordVisitResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RescheduleVisitRequest
{
    public string? ScheduledAt { get; init; }

    public string? Note { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LandlordCancelVisitRequest
{
    public string? Reason { get; init; }

    public string? Note { get; init; }
}

public sealed record VisitListResponse(
    IReadOnlyList<VisitResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

// Resultado de crear: `Replayed` indica que se devolvió la visita original
// de la misma Idempotency-Key.
public sealed record CreateVisitResult(VisitResponse Visit, bool Replayed);
