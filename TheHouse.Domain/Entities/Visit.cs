namespace TheHouse.Domain.Entities;

// NV-17: estados de una visita. En 1.3.0 se emiten Requested, Confirmed y
// Cancelled; Rescheduled solo aparece en el historial. Rejected y
// Completed quedan reservados.
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

public static class VisitActors
{
    public const string Student = "student";

    public const string Landlord = "landlord";
}

// NV-17: visita de un estudiante a un cuarto. 1.3.0 agrega la parte del
// arrendador (confirmar, reprogramar, cancelar). Las visitas de 1.1.0 aún no
// migradas solo tienen `LegacyPropertyId`.
public sealed class Visit
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string? LegacyPropertyId { get; init; }

    // Siempre en UTC.
    public DateTimeOffset ScheduledAt { get; init; }

    public DateTimeOffset? PreviousScheduledAt { get; init; }

    public VisitStatus Status { get; init; }

    public string? Message { get; init; }

    public string? CancelReason { get; init; }

    public string? CancelNote { get; init; }

    public string? CancelledBy { get; init; }

    public string? RescheduleNote { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public IReadOnlyList<VisitHistoryEntry> History { get; init; } =
        Array.Empty<VisitHistoryEntry>();

    // Activa = solicitada o confirmada y con horario futuro: es la que cuenta
    // para conflictos y límites.
    public bool IsActive(DateTimeOffset now) =>
        Status is VisitStatus.Requested or VisitStatus.Confirmed &&
        ScheduledAt > now;

    // Estudiante: Requested o Confirmed → Cancelled antes del horario.
    // Cancelar una ya cancelada es idempotente.
    public Visit Cancel(string? reason, DateTimeOffset now)
    {
        if (Status == VisitStatus.Cancelled)
        {
            return this;
        }

        EnsureChangeable(now);
        return With(
            status: VisitStatus.Cancelled,
            now: now,
            actor: VisitActors.Student,
            history: VisitStatus.Cancelled,
            cancelReason: reason,
            cancelledBy: VisitActors.Student,
            cancelledAt: now);
    }

    // Arrendador: Requested → Confirmed antes del horario.
    public Visit Confirm(DateTimeOffset now)
    {
        if (Status != VisitStatus.Requested)
        {
            throw new Exceptions.ConflictException("visit_state");
        }

        EnsureChangeable(now);
        return With(status: VisitStatus.Confirmed, now: now, actor: VisitActors.Landlord, history: VisitStatus.Confirmed, confirmedAt: now);
    }

    // Arrendador: Requested o Confirmed → Confirmed en otro horario.
    public Visit Reschedule(DateTimeOffset scheduledAt, string? note, DateTimeOffset now)
    {
        EnsureChangeable(now);
        return With(
            status: VisitStatus.Confirmed,
            now: now,
            actor: VisitActors.Landlord,
            history: VisitStatus.Rescheduled,
            scheduledAt: scheduledAt,
            previousScheduledAt: ScheduledAt,
            rescheduleNote: note,
            confirmedAt: now);
    }

    // Arrendador: Requested o Confirmed → Cancelled con motivo.
    public Visit CancelByLandlord(string reason, string? note, DateTimeOffset now)
    {
        EnsureChangeable(now);
        return With(
            status: VisitStatus.Cancelled,
            now: now,
            actor: VisitActors.Landlord,
            history: VisitStatus.Cancelled,
            cancelReason: reason,
            cancelNote: note,
            cancelledBy: VisitActors.Landlord,
            cancelledAt: now);
    }

    private void EnsureChangeable(DateTimeOffset now)
    {
        if (Status is not (VisitStatus.Requested or VisitStatus.Confirmed))
        {
            throw new Exceptions.ConflictException("visit_state");
        }

        if (ScheduledAt <= now)
        {
            throw new Exceptions.ConflictException("visit_in_past");
        }
    }

    private Visit With(
        VisitStatus status,
        DateTimeOffset now,
        string actor,
        VisitStatus history,
        DateTimeOffset? scheduledAt = null,
        DateTimeOffset? previousScheduledAt = null,
        string? cancelReason = null,
        string? cancelNote = null,
        string? cancelledBy = null,
        string? rescheduleNote = null,
        DateTimeOffset? confirmedAt = null,
        DateTimeOffset? cancelledAt = null) =>
        new()
        {
            Id = Id,
            UserId = UserId,
            RoomId = RoomId,
            LegacyPropertyId = LegacyPropertyId,
            ScheduledAt = scheduledAt ?? ScheduledAt,
            PreviousScheduledAt = previousScheduledAt ?? PreviousScheduledAt,
            Status = status,
            Message = Message,
            CancelReason = cancelReason ?? CancelReason,
            CancelNote = cancelNote ?? CancelNote,
            CancelledBy = cancelledBy ?? CancelledBy,
            RescheduleNote = rescheduleNote ?? RescheduleNote,
            CreatedAt = CreatedAt,
            UpdatedAt = now,
            ConfirmedAt = confirmedAt ?? ConfirmedAt,
            CancelledAt = cancelledAt ?? CancelledAt,
            History = [.. History, new VisitHistoryEntry(history, now, actor)],
        };
}
