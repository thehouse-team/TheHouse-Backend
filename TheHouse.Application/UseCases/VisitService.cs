using System.Security.Cryptography;
using System.Text;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-17 v1: solicitudes de visita del estudiante.
//  - Idempotency-Key: misma llave y mismo cuerpo → la visita original;
//    cuerpo distinto → 409.
//  - Creación serializada por usuario con lock distribuido (réplicas).
//  - Conflictos: una activa por propiedad, 60 min de separación, máximo 5.
public sealed class VisitService(
    IVisitRepository visits,
    IPropertyRepository properties,
    IUserLock userLock,
    TimeProvider timeProvider)
{
    public const int MaximumActiveVisits = 5;

    public const int MaximumMessageLength = 300;

    public const int MaximumReasonLength = 200;

    public static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5);

    public async Task<CreateVisitResult> CreateAsync(
        string userId,
        string? idempotencyKey,
        CreateVisitRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idempotencyKey, out var key))
        {
            throw new RequestValidationException("Idempotency-Key", "Idempotency-Key must be a UUID.");
        }

        if (!PropertyQueryValidation.IsObjectId(request.PropertyId))
        {
            throw new RequestValidationException("propertyId", "propertyId must be a 24-character hexadecimal ObjectId.");
        }

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
        if (message is { Length: > MaximumMessageLength })
        {
            throw new RequestValidationException("message", "message is too long.");
        }

        var propertyId = request.PropertyId!.ToLowerInvariant();
        var scheduledAt = VisitSchedulePolicy.ParseScheduledAt(request.ScheduledAt);
        var keyText = key.ToString("D");
        var requestHash = Hash($"{propertyId}\n{scheduledAt.UtcTicks}\n{message}");

        await using var held = await userLock.TryAcquireAsync(
            RateLimitKeys.VisitLock(userId),
            LockTtl,
            cancellationToken) ?? throw new ConflictException("visit_in_progress");

        var previous = await visits.FindIdempotencyAsync(userId, keyText, cancellationToken);
        if (previous is not null)
        {
            if (!string.Equals(previous.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new ConflictException("idempotency_key_reused");
            }

            var original = await visits.GetAsync(previous.VisitId, cancellationToken)
                ?? throw new ConflictException("idempotency_key_reused");
            return new CreateVisitResult(await ToResponseAsync(original, cancellationToken), Replayed: true);
        }

        var property = await properties.GetByIdAsync(propertyId, cancellationToken)
            ?? throw new NotFoundException("property");

        var now = timeProvider.GetUtcNow();
        VisitSchedulePolicy.EnsureAllowed(scheduledAt, now);

        var active = await visits.ListActiveAsync(userId, now, cancellationToken);
        if (active.Any(visit => visit.PropertyId == propertyId))
        {
            throw new ConflictException("active_visit_for_property");
        }

        if (active.Any(visit => (visit.ScheduledAt - scheduledAt).Duration() < VisitSchedulePolicy.ConflictWindow))
        {
            throw new ConflictException("schedule_overlap");
        }

        if (active.Count >= MaximumActiveVisits)
        {
            throw new ConflictException("active_visits_limit");
        }

        var created = await visits.CreateAsync(
            new Visit
            {
                UserId = userId,
                PropertyId = propertyId,
                ScheduledAt = scheduledAt,
                Status = VisitStatus.Requested,
                Message = message,
                CreatedAt = now,
                UpdatedAt = now,
                History = [new VisitHistoryEntry(VisitStatus.Requested, now, "student")],
            },
            cancellationToken);

        await visits.SaveIdempotencyAsync(
            new VisitIdempotencyRecord(userId, keyText, requestHash, created.Id, now),
            cancellationToken);

        return new CreateVisitResult(ToResponse(created, property), Replayed: false);
    }

    public async Task<VisitResponse> GetAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var visit = await LoadOwnAsync(userId, id, cancellationToken);
        return await ToResponseAsync(visit, cancellationToken);
    }

    public async Task<VisitListResponse> ListAsync(
        string userId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);

        VisitStatus? filter = status switch
        {
            null or "" => null,
            "requested" => VisitStatus.Requested,
            "cancelled" => VisitStatus.Cancelled,
            _ => throw new RequestValidationException("status", "status must be requested or cancelled."),
        };

        var (items, total) = await visits.ListAsync(userId, filter, page, pageSize, cancellationToken);
        var loaded = await properties.GetByIdsAsync(items.Select(visit => visit.PropertyId).Distinct().ToArray(), cancellationToken);
        var byId = loaded.ToDictionary(property => property.Id, StringComparer.Ordinal);

        return new VisitListResponse(
            items.Select(visit => ToResponse(visit, byId.GetValueOrDefault(visit.PropertyId))).ToArray(),
            page,
            pageSize,
            total,
            PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<VisitResponse> CancelAsync(
        string userId,
        string? id,
        string? reason,
        CancellationToken cancellationToken)
    {
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (cleanReason is { Length: > MaximumReasonLength })
        {
            throw new RequestValidationException("reason", "reason is too long.");
        }

        var visit = await LoadOwnAsync(userId, id, cancellationToken);
        var cancelled = visit.Cancel(cleanReason, timeProvider.GetUtcNow());

        if (!ReferenceEquals(cancelled, visit))
        {
            await visits.UpdateAsync(cancelled, cancellationToken);
        }

        return await ToResponseAsync(cancelled, cancellationToken);
    }

    private async Task<Visit> LoadOwnAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        if (!PropertyQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException("id", "id must be a 24-character hexadecimal ObjectId.");
        }

        var visit = await visits.GetAsync(id!.ToLowerInvariant(), cancellationToken);

        // Una visita ajena responde igual que una inexistente (BOLA).
        if (visit is null || !string.Equals(visit.UserId, userId, StringComparison.Ordinal))
        {
            throw new NotFoundException("visit");
        }

        return visit;
    }

    private async Task<VisitResponse> ToResponseAsync(Visit visit, CancellationToken cancellationToken) =>
        ToResponse(visit, await properties.GetByIdAsync(visit.PropertyId, cancellationToken));

    private static VisitResponse ToResponse(Visit visit, Property? property) =>
        new(
            visit.Id,
            new VisitPropertySummary(visit.PropertyId, property?.Title ?? string.Empty, property?.City ?? string.Empty),
            visit.ScheduledAt.ToUniversalTime(),
            VisitSchedulePolicy.TimeZoneId,
            visit.Status.ToString().ToLowerInvariant(),
            visit.Message,
            visit.CreatedAt,
            visit.UpdatedAt,
            visit.CancelledAt);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
