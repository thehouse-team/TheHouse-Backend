using System.Security.Cryptography;
using System.Text;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-17 1.2.0: solicitudes de visita del estudiante a cuartos publicados.
//  - Idempotency-Key: misma llave y mismo cuerpo → la visita original;
//    cuerpo distinto → 409. El hash usa el `roomId` ya traducido.
//  - Creación serializada por usuario con lock distribuido (réplicas).
//  - Conflictos por cuarto: una activa por cuarto, 60 min de separación,
//    máximo 5. Los códigos de 1.1.0 no cambian (Mobile los usa).
//  - Transición: `propertyId` de NV-07 se traduce a su cuarto.
public sealed class VisitService(
    IVisitRepository visits,
    RoomCatalog catalog,
    ILandlordProfileRepository landlords,
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

        if ((request.RoomId is null) == (request.PropertyId is null))
        {
            throw new RequestValidationException("roomId", "Send exactly one of roomId or propertyId.");
        }

        var requested = request.RoomId ?? request.PropertyId;
        if (!PropertyQueryValidation.IsObjectId(requested))
        {
            throw new RequestValidationException("roomId", "roomId must be a 24-character hexadecimal ObjectId.");
        }

        var message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim();
        if (message is { Length: > MaximumMessageLength })
        {
            throw new RequestValidationException("message", "message is too long.");
        }

        var scheduledAt = VisitSchedulePolicy.ParseScheduledAt(request.ScheduledAt);
        var room = request.RoomId is not null
            ? await catalog.FindAsync(requested!.ToLowerInvariant(), cancellationToken)
            : await catalog.FromLegacyAsync(requested!.ToLowerInvariant(), cancellationToken);
        if (room is null)
        {
            throw new NotFoundException("room");
        }

        var roomId = room.Room.Id;
        var keyText = key.ToString("D");
        var requestHash = Hash($"{roomId}\n{scheduledAt.UtcTicks}\n{message}");

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

        if (!room.IsPublished)
        {
            throw new NotFoundException("room");
        }

        var now = timeProvider.GetUtcNow();
        VisitSchedulePolicy.EnsureAllowed(scheduledAt, now);

        var active = await visits.ListActiveAsync(userId, now, cancellationToken);
        var legacy = active.Any(visit => visit.RoomId.Length == 0)
            ? await catalog.LegacyMapAsync(cancellationToken)
            : new Dictionary<string, string>();
        if (active.Any(visit => EffectiveRoomId(visit, legacy) == roomId))
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
                RoomId = roomId,
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

        return new CreateVisitResult(ToResponse(created, room, await LandlordNameAsync(room.Building.OwnerId, cancellationToken)), Replayed: false);
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
            "confirmed" => VisitStatus.Confirmed,
            "cancelled" => VisitStatus.Cancelled,
            _ => throw new RequestValidationException("status", "status must be requested, confirmed or cancelled."),
        };

        var (items, total) = await visits.ListAsync(userId, filter, page, pageSize, cancellationToken);
        var legacy = items.Any(visit => visit.RoomId.Length == 0)
            ? await catalog.LegacyMapAsync(cancellationToken)
            : new Dictionary<string, string>();
        var rooms = await catalog.LoadAsync(items.Select(visit => EffectiveRoomId(visit, legacy)), cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var owner in rooms.Values.Select(room => room.Building.OwnerId).Distinct(StringComparer.Ordinal))
        {
            names[owner] = await LandlordNameAsync(owner, cancellationToken);
        }

        return new VisitListResponse(
            items.Select(visit =>
            {
                var room = rooms.GetValueOrDefault(EffectiveRoomId(visit, legacy));
                return ToResponse(visit, room, room is null ? MessagingRules.FirstName(null) : names[room.Building.OwnerId]);
            }).ToArray(),
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

        // Actualización condicional: si el arrendador la cambió al mismo tiempo,
        // se vuelve a leer una vez y se aplica sobre el estado nuevo.
        for (var attempt = 0; ; attempt++)
        {
            var visit = await LoadOwnAsync(userId, id, cancellationToken);
            var cancelled = visit.Cancel(cleanReason, timeProvider.GetUtcNow());
            if (ReferenceEquals(cancelled, visit) || await visits.ReplaceIfUnchangedAsync(cancelled, visit.UpdatedAt, cancellationToken))
            {
                return await ToResponseAsync(cancelled, cancellationToken);
            }

            if (attempt == 1)
            {
                throw new ConflictException("visit_state");
            }
        }
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

    private async Task<VisitResponse> ToResponseAsync(Visit visit, CancellationToken cancellationToken)
    {
        var room = visit.RoomId.Length > 0
            ? await catalog.FindAsync(visit.RoomId, cancellationToken)
            : await catalog.FromLegacyAsync(visit.LegacyPropertyId ?? string.Empty, cancellationToken);
        var name = room is null ? MessagingRules.FirstName(null) : await LandlordNameAsync(room.Building.OwnerId, cancellationToken);
        return ToResponse(visit, room, name);
    }

    private async Task<string> LandlordNameAsync(string ownerId, CancellationToken cancellationToken) =>
        MessagingRules.FirstName((await landlords.GetAsync(ownerId, cancellationToken))?.FullName);

    // La dirección exacta solo con la visita confirmada (RNF-02).
    private static VisitAddress? AddressOf(Visit visit, CatalogRoom? room) =>
        visit.Status == VisitStatus.Confirmed && room is not null
            ? new VisitAddress(
                room.Building.Address.Street,
                room.Building.Address.Number,
                room.Building.Address.Neighborhood,
                room.Building.Address.City,
                room.Building.Address.State,
                room.Building.Address.PostalCode)
            : null;

    private static VisitLocation? LocationOf(Visit visit, CatalogRoom? room) =>
        visit.Status == VisitStatus.Confirmed && room is not null
            ? new VisitLocation(room.Building.Latitude, room.Building.Longitude)
            : null;

    public static VisitCancellation? CancellationOf(Visit visit) =>
        visit.Status == VisitStatus.Cancelled
            ? new VisitCancellation(visit.CancelledBy ?? VisitActors.Student, visit.CancelReason, visit.CancelNote)
            : null;

    private static string EffectiveRoomId(Visit visit, IReadOnlyDictionary<string, string> legacy) =>
        visit.RoomId.Length > 0 ? visit.RoomId : legacy.GetValueOrDefault(visit.LegacyPropertyId ?? string.Empty) ?? string.Empty;

    // Las visitas de cuartos que dejaron de publicarse se siguen mostrando.
    private static VisitResponse ToResponse(Visit visit, CatalogRoom? room, string landlordName) =>
        new(
            visit.Id,
            new VisitRoomSummary(
                room?.Room.Id ?? visit.RoomId,
                room?.Building.Id ?? string.Empty,
                room?.Title ?? string.Empty,
                room?.Building.Address.City ?? string.Empty),
            room?.LegacyPropertyId is { } propertyId
                ? new VisitPropertySummary(propertyId, room.Building.Name, room.Building.Address.City)
                : null,
            visit.ScheduledAt.ToUniversalTime(),
            visit.PreviousScheduledAt?.ToUniversalTime(),
            VisitSchedulePolicy.TimeZoneId,
            visit.Status.ToString().ToLowerInvariant(),
            visit.Message,
            new VisitLandlordSummary(landlordName),
            AddressOf(visit, room),
            LocationOf(visit, room),
            CancellationOf(visit),
            visit.CreatedAt,
            visit.UpdatedAt,
            visit.ConfirmedAt,
            visit.CancelledAt);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
