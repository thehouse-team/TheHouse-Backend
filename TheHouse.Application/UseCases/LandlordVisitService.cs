using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-17 1.3.0 (RF-80): el arrendador ve las visitas de sus cuartos y las
// confirma, reprograma o cancela. Una visita de un cuarto ajeno responde 404.
public sealed class LandlordVisitService(
    IVisitRepository visits,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    ILandlordProfileRepository landlords,
    IProfileRepository profiles,
    IUniversityRepository universities,
    TimeProvider timeProvider,
    INotificationSink? notifications = null)
{
    public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(30);

    public static readonly TimeSpan MaximumRange = TimeSpan.FromDays(62);

    public const int MaximumNoteLength = 300;

    public static readonly IReadOnlySet<string> CancelReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "not_available", "schedule_conflict", "other",
    };

    public async Task<LandlordVisitListResponse> CalendarAsync(
        string ownerId,
        string? from,
        string? to,
        string? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        VisitStatus? filter = status switch
        {
            null or "" => null,
            "requested" => VisitStatus.Requested,
            "confirmed" => VisitStatus.Confirmed,
            "cancelled" => VisitStatus.Cancelled,
            _ => throw new RequestValidationException("status", "status must be requested, confirmed or cancelled."),
        };

        var start = string.IsNullOrEmpty(from) ? StartOfToday() : Parse(from, "from");
        var end = string.IsNullOrEmpty(to) ? start + DefaultRange : Parse(to, "to");
        if (end < start || end - start > MaximumRange)
        {
            throw new RequestValidationException("to", "to must be after from and at most 62 days later.");
        }

        if (roomId is not null && !PropertyQueryValidation.IsObjectId(roomId))
        {
            throw new RequestValidationException("roomId", "roomId must be a 24-character hexadecimal ObjectId.");
        }

        var owned = await rooms.ListAllByOwnerAsync(ownerId, cancellationToken);
        var roomIds = owned.Where(room => roomId is null || room.Id == roomId).Select(room => room.Id).ToArray();
        if (roomIds.Length == 0)
        {
            return new LandlordVisitListResponse([], page, pageSize, 0, 0);
        }

        var (items, total) = await visits.ListForRoomsAsync(roomIds, start, end, filter, page, pageSize, cancellationToken);
        var byId = owned.ToDictionary(room => room.Id, StringComparer.Ordinal);
        var responses = new List<LandlordVisitResponse>(items.Count);
        foreach (var visit in items)
        {
            responses.Add(await ToResponseAsync(visit, byId[visit.RoomId], cancellationToken));
        }

        return new LandlordVisitListResponse(responses, page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<LandlordVisitResponse> GetAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var (visit, room) = await LoadOwnedAsync(ownerId, id, cancellationToken);
        return await ToResponseAsync(visit, room, cancellationToken);
    }

    public async Task<LandlordVisitResponse> ConfirmAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var (visit, room) = await LoadOwnedAsync(ownerId, id, cancellationToken);
        await EnsurePublishedAsync(room, cancellationToken);
        var confirmed = visit.Confirm(timeProvider.GetUtcNow());
        var response = await SaveAsync(visit, confirmed, room, cancellationToken);
        await NotifyStudentAsync(NotificationTypes.VisitConfirmed, confirmed, ownerId, $"{NotificationTypes.VisitConfirmed}:{visit.Id}", cancellationToken);
        return response;
    }

    public async Task<LandlordVisitResponse> RescheduleAsync(string ownerId, string? id, RescheduleVisitRequest body, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var note = Note(body.Note, required: false);
        var scheduledAt = VisitSchedulePolicy.ParseScheduledAt(body.ScheduledAt);
        var (visit, room) = await LoadOwnedAsync(ownerId, id, cancellationToken);
        await EnsurePublishedAsync(room, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var moved = visit.Reschedule(scheduledAt, note, now);
        VisitSchedulePolicy.EnsureAllowed(scheduledAt, now);
        var others = await visits.ListActiveAsync(visit.UserId, now, cancellationToken);
        if (others.Any(other => other.Id != visit.Id && (other.ScheduledAt - scheduledAt).Duration() < VisitSchedulePolicy.ConflictWindow))
        {
            throw new ConflictException("student_schedule_overlap");
        }

        var rescheduled = await SaveAsync(visit, moved, room, cancellationToken);
        await NotifyStudentAsync(
            NotificationTypes.VisitRescheduled,
            moved,
            ownerId,
            $"{NotificationTypes.VisitRescheduled}:{visit.Id}:{moved.UpdatedAt.UtcTicks}",
            cancellationToken);
        return rescheduled;
    }

    public async Task<LandlordVisitResponse> CancelAsync(string ownerId, string? id, LandlordCancelVisitRequest body, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        if (body.Reason is null || !CancelReasons.Contains(body.Reason))
        {
            throw new RequestValidationException("reason", "reason must be not_available, schedule_conflict or other.");
        }

        var note = Note(body.Note, required: body.Reason == "other");
        var (visit, room) = await LoadOwnedAsync(ownerId, id, cancellationToken);
        var cancelled = visit.CancelByLandlord(body.Reason, note, timeProvider.GetUtcNow());
        var response = await SaveAsync(visit, cancelled, room, cancellationToken);
        await NotifyStudentAsync(NotificationTypes.VisitCancelled, cancelled, ownerId, $"{NotificationTypes.VisitCancelled}:{visit.Id}", cancellationToken);
        return response;
    }

    private async Task<LandlordVisitResponse> SaveAsync(Visit previous, Visit updated, Room room, CancellationToken cancellationToken)
    {
        if (!await visits.ReplaceIfUnchangedAsync(updated, previous.UpdatedAt, cancellationToken))
        {
            throw new ConflictException("visit_state");
        }

        return await ToResponseAsync(updated, room, cancellationToken);
    }

    // NV-20: aviso al estudiante después de guardar.
    private Task NotifyStudentAsync(string type, Visit visit, string ownerId, string key, CancellationToken cancellationToken) =>
        notifications?.NotifyAsync(
            new NotificationEvent(type, visit.UserId, visit.Id, key, RoomId: visit.RoomId, LandlordId: ownerId, At: visit.ScheduledAt),
            cancellationToken) ?? Task.CompletedTask;

    private static string? Note(string? value, bool required)
    {
        var note = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (note is { Length: > MaximumNoteLength })
        {
            throw new RequestValidationException("note", "note must have at most 300 characters.");
        }

        if (required && note is null)
        {
            throw new RequestValidationException("note", "note is required when reason is other.");
        }

        return note;
    }

    private static DateTimeOffset Parse(string value, string field)
    {
        try
        {
            return VisitSchedulePolicy.ParseScheduledAt(value);
        }
        catch (RequestValidationException)
        {
            throw new RequestValidationException(field, $"{field} must be an ISO 8601 date and time with offset.");
        }
    }

    private DateTimeOffset StartOfToday()
    {
        var local = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VisitSchedulePolicy.MexicoCity);
        return new DateTimeOffset(local.Date, local.Offset).ToUniversalTime();
    }

    private async Task RequireLandlordAsync(string userId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(userId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }
    }

    private async Task<(Visit Visit, Room Room)> LoadOwnedAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        if (!PropertyQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException("id", "id must be a 24-character hexadecimal ObjectId.");
        }

        var visit = await visits.GetAsync(id!.ToLowerInvariant(), cancellationToken);
        if (visit is null || visit.RoomId.Length == 0)
        {
            throw new NotFoundException("visit");
        }

        var room = await rooms.GetAsync(visit.RoomId, cancellationToken);
        return room is not null && room.OwnerId == ownerId ? (visit, room) : throw new NotFoundException("visit");
    }

    // Confirmar o reprogramar solo sobre cuartos que siguen publicados.
    private async Task EnsurePublishedAsync(Room room, CancellationToken cancellationToken)
    {
        var building = await buildings.GetAsync(room.BuildingId, cancellationToken);
        if (room.Status != RoomStatus.Available || building?.Status != BuildingStatus.Verified)
        {
            throw new ConflictException("visit_state");
        }
    }

    private async Task<LandlordVisitResponse> ToResponseAsync(Visit visit, Room room, CancellationToken cancellationToken)
    {
        var building = await buildings.GetAsync(room.BuildingId, cancellationToken);
        var profile = await profiles.GetAsync(visit.UserId, cancellationToken);
        string? university = null;
        if (profile?.UniversityId is { } universityId && await universities.GetByIdAsync(universityId, cancellationToken) is { } found)
        {
            university = found.Name;
        }

        return new LandlordVisitResponse(
            visit.Id,
            new VisitRoomSummary(room.Id, room.BuildingId, building is null ? "Cuarto" : $"{building.Name} · Cuarto {room.Number}", building?.Address.City ?? string.Empty),
            visit.ScheduledAt.ToUniversalTime(),
            visit.PreviousScheduledAt?.ToUniversalTime(),
            VisitSchedulePolicy.TimeZoneId,
            visit.Status.ToString().ToLowerInvariant(),
            visit.Message,
            new VisitStudentSummary(string.IsNullOrWhiteSpace(profile?.Name) ? null : profile!.Name, university),
            VisitService.CancellationOf(visit),
            visit.CreatedAt,
            visit.UpdatedAt,
            visit.ConfirmedAt,
            visit.CancelledAt);
    }
}
