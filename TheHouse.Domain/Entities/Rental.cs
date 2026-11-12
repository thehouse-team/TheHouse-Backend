using TheHouse.Domain.Exceptions;

namespace TheHouse.Domain.Entities;

// NV-18: convenio entre arrendador y estudiante. `Pending` espera que el
// estudiante acepte con el código; `Active` es la renta; `Ended` y
// `Cancelled` quedan como historial.
public enum AgreementStatus
{
    Pending,
    Active,
    Ended,
    Cancelled,
}

public static class AgreementActors
{
    public const string Student = "student";

    public const string Landlord = "landlord";
}

// Términos del convenio en MXN; `EndDate` = `StartDate` + `DurationMonths`.
public sealed record AgreementTerms(
    int MonthlyRent,
    int Deposit,
    DateOnly StartDate,
    int DurationMonths,
    DateOnly EndDate,
    int PaymentDay);

// Código de cierre vigente. Solo se guardan los hashes; el código en claro
// sale una vez en la respuesta de crear o de `/code`.
public sealed record ClosingCode(
    string CodeHash,
    string QrTokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? UsedAt,
    int FailedAttempts)
{
    public const int MaximumFailedAttempts = 5;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public bool IsBlocked => FailedAttempts >= MaximumFailedAttempts;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;
}

public sealed record AgreementHistoryEntry(AgreementStatus Status, DateTimeOffset At, string Actor);

public sealed record Agreement
{
    public string Id { get; init; } = string.Empty;

    public string RequestId { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string StudentId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public AgreementTerms Terms { get; init; } = new(0, 0, default, 0, default, 1);

    public AgreementStatus Status { get; init; } = AgreementStatus.Pending;

    public ClosingCode? Code { get; init; }

    public string? EndReason { get; init; }

    public string? EndNote { get; init; }

    public IReadOnlyList<AgreementHistoryEntry> History { get; init; } = Array.Empty<AgreementHistoryEntry>();

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ActivatedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    // Abierto = pendiente o activo: uno por cuarto (índice único parcial).
    public bool IsOpen => Status is AgreementStatus.Pending or AgreementStatus.Active;

    // Código nuevo: solo sobre un convenio pendiente; invalida el anterior.
    public Agreement WithCode(ClosingCode code, DateTimeOffset now) =>
        Status == AgreementStatus.Pending
            ? this with { Code = code, UpdatedAt = now }
            : throw new ConflictException("agreement_state");

    // Revisa que el código siga sirviendo: inexistente o bloqueado → 404
    // `code_invalid`; usado → 410 `code_used`; vencido → 410 `code_expired`.
    public void EnsureCodeUsable(DateTimeOffset now)
    {
        if (Code is null || Code.IsBlocked)
        {
            throw new NotFoundException("code", "code_invalid");
        }

        if (Code.UsedAt is not null || Status != AgreementStatus.Pending)
        {
            throw Status is AgreementStatus.Pending or AgreementStatus.Active
                ? new GoneException("code_used")
                : new NotFoundException("code", "code_invalid");
        }

        if (Code.IsExpired(now))
        {
            throw new GoneException("code_expired");
        }
    }

    // Estudiante: pendiente → activo y el código queda usado.
    public Agreement Activate(DateTimeOffset now)
    {
        EnsureCodeUsable(now);
        return this with
        {
            Status = AgreementStatus.Active,
            Code = Code! with { UsedAt = now },
            ActivatedAt = now,
            UpdatedAt = now,
            History = [.. History, new AgreementHistoryEntry(AgreementStatus.Active, now, AgreementActors.Student)],
        };
    }

    // Compensación: el cuarto no se pudo marcar rentado; vuelve a pendiente
    // sin código para que el arrendador genere otro.
    public Agreement RevertActivation(DateTimeOffset now) =>
        this with
        {
            Status = AgreementStatus.Pending,
            Code = null,
            ActivatedAt = null,
            UpdatedAt = now,
            History = [.. History, new AgreementHistoryEntry(AgreementStatus.Pending, now, AgreementActors.Student)],
        };

    // Arrendador: solo un convenio pendiente; el código deja de servir.
    public Agreement Cancel(DateTimeOffset now) =>
        Status == AgreementStatus.Pending
            ? this with
            {
                Status = AgreementStatus.Cancelled,
                Code = null,
                CancelledAt = now,
                UpdatedAt = now,
                History = [.. History, new AgreementHistoryEntry(AgreementStatus.Cancelled, now, AgreementActors.Landlord)],
            }
            : throw new ConflictException("agreement_state");

    // Arrendador: solo una renta activa.
    public Agreement End(string? reason, string? note, DateTimeOffset now) =>
        Status == AgreementStatus.Active
            ? this with
            {
                Status = AgreementStatus.Ended,
                EndReason = reason,
                EndNote = note,
                EndedAt = now,
                UpdatedAt = now,
                History = [.. History, new AgreementHistoryEntry(AgreementStatus.Ended, now, AgreementActors.Landlord)],
            }
            : throw new ConflictException("agreement_state");
}
