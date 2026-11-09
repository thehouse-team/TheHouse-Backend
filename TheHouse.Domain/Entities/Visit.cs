namespace TheHouse.Domain.Entities;

// NV-17: estados de una visita. En v1 solo se emiten Requested y
// Cancelled; los demás quedan reservados para la versión con
// arrendadores (NV-12) y no deben producirse todavía.
public enum VisitStatus
{
    Requested,
    Cancelled,
    Confirmed,
    Rejected,
    Rescheduled,
    Completed,
}

public sealed record VisitHistoryEntry(
    VisitStatus Status,
    DateTimeOffset At,
    string Actor);

// NV-17: solicitud de visita de un estudiante a un cuarto (1.2.0). Las
// visitas de 1.1.0 aún no migradas solo tienen `LegacyPropertyId`.
public sealed class Visit
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string? LegacyPropertyId { get; init; }

    // Siempre en UTC.
    public DateTimeOffset ScheduledAt { get; init; }

    public VisitStatus Status { get; init; }

    public string? Message { get; init; }

    public string? CancelReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public IReadOnlyList<VisitHistoryEntry> History { get; init; } =
        Array.Empty<VisitHistoryEntry>();

    // Activa = solicitada y con horario futuro: es la que cuenta para
    // conflictos y límites.
    public bool IsActive(DateTimeOffset now) =>
        Status == VisitStatus.Requested &&
        ScheduledAt > now;

    // Única transición de v1: Requested → Cancelled, por el estudiante y
    // antes del horario. Cancelar una ya cancelada es idempotente.
    public Visit Cancel(string? reason, DateTimeOffset now)
    {
        if (Status == VisitStatus.Cancelled)
        {
            return this;
        }

        if (Status != VisitStatus.Requested)
        {
            throw new Exceptions.ConflictException("visit_state");
        }

        if (ScheduledAt <= now)
        {
            throw new Exceptions.ConflictException("visit_in_past");
        }

        return new Visit
        {
            Id = Id,
            UserId = UserId,
            RoomId = RoomId,
            LegacyPropertyId = LegacyPropertyId,
            ScheduledAt = ScheduledAt,
            Status = VisitStatus.Cancelled,
            Message = Message,
            CancelReason = reason,
            CreatedAt = CreatedAt,
            UpdatedAt = now,
            CancelledAt = now,
            History = [.. History, new VisitHistoryEntry(VisitStatus.Cancelled, now, "student")],
        };
    }
}
