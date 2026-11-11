using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-16: ids con el formato de ObjectId (4 bytes de segundos + 8 aleatorios)
// para crear la solicitud y su conversación ya enlazadas.
public static class MessagingIds
{
    public static string New(DateTimeOffset now)
    {
        Span<byte> bytes = stackalloc byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)now.ToUnixTimeSeconds());
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes[4..]);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// NV-16: reglas compartidas de solicitudes y chat.
public static class MessagingRules
{
    public static readonly IReadOnlySet<string> QuickQuestions = new HashSet<string>(StringComparer.Ordinal)
    {
        "available_now", "services_included", "deposit", "minimum_contract", "visit_possible", "pets_allowed", "roommates",
    };

    public static readonly IReadOnlySet<string> RejectionReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "not_available", "profile_mismatch", "other",
    };

    public const int MaximumQuickQuestions = 5;

    public const int MaximumRequestMessage = 500;

    public const int MaximumRejectionNote = 300;

    public const int MaximumMessageText = 1000;

    public const int MaximumActiveRequests = 10;

    public const int MaximumMessagesPerPage = 50;

    public const int UnreadCap = 99;

    public const int PollAfterSeconds = 10;

    public static string Wire(RentalRequestStatus status) => status.ToString().ToLowerInvariant();

    public static RentalRequestStatus? ParseStatus(string? value) => value switch
    {
        null => null,
        "pending" => RentalRequestStatus.Pending,
        "accepted" => RentalRequestStatus.Accepted,
        "rejected" => RentalRequestStatus.Rejected,
        "cancelled" => RentalRequestStatus.Cancelled,
        _ => throw new RequestValidationException("status", "status must be pending, accepted, rejected or cancelled."),
    };

    public static void EnsureId(string? value, string field)
    {
        if (!PropertyQueryValidation.IsObjectId(value))
        {
            throw new RequestValidationException(field, $"{field} must be a 24-character hexadecimal ObjectId.");
        }
    }

    // Primer nombre del arrendador (NV-12); nunca apellidos ni teléfono.
    public static string FirstName(string? fullName)
    {
        var first = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "Arrendador" : first;
    }
}

// NV-16: solicitudes de interés (RF-34, RF-35).
public sealed class RentalRequestService(
    IRentalRequestRepository requests,
    IConversationRepository conversations,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    ILandlordProfileRepository landlords,
    IProfileRepository profiles,
    IUniversityRepository universities,
    IVisitRepository visits,
    IAccountSuspensionQuery suspensions,
    IUserLock userLock,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(10);

    public async Task<RentalRequestResponse> CreateAsync(string studentId, string roomId, CreateRentalRequest body, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(roomId, "roomId");
        var questions = (body.QuickQuestions ?? Array.Empty<string>()).ToArray();
        if (questions.Length > MessagingRules.MaximumQuickQuestions ||
            questions.Any(question => !MessagingRules.QuickQuestions.Contains(question)) ||
            questions.Distinct(StringComparer.Ordinal).Count() != questions.Length)
        {
            throw new RequestValidationException("quickQuestions", "quickQuestions must contain up to 5 distinct values of the catalog.");
        }

        var message = body.Message?.Trim();
        if (message is { Length: > MessagingRules.MaximumRequestMessage })
        {
            throw new RequestValidationException("message", "message must have at most 500 characters.");
        }

        if (string.IsNullOrEmpty(message))
        {
            message = null;
        }

        if (questions.Length == 0 && message is null)
        {
            throw new RequestValidationException("message", "Send at least one quick question or a message.");
        }

        var room = await rooms.GetAsync(roomId, cancellationToken) ?? throw new NotFoundException("room");
        var building = await buildings.GetAsync(room.BuildingId, cancellationToken) ?? throw new NotFoundException("room");
        if (room.Status != RoomStatus.Available || building.Status != BuildingStatus.Verified)
        {
            throw new NotFoundException("room");
        }

        if (room.OwnerId == studentId)
        {
            throw new BusinessRuleException("own_room");
        }

        await EnsureNotSuspendedAsync(studentId, room.OwnerId, cancellationToken);

        await using var held = await userLock.TryAcquireAsync(RateLimitKeys.MessagingLock(studentId), LockTtl, cancellationToken)
            ?? throw new ConflictException("request_in_progress");

        if (await requests.CountActiveByStudentAsync(studentId, cancellationToken) >= MessagingRules.MaximumActiveRequests)
        {
            throw new ConflictException("active_requests_limit");
        }

        var now = timeProvider.GetUtcNow();
        var requestId = MessagingIds.New(now);
        var conversationId = MessagingIds.New(now);

        // Primero la solicitud: su índice único parcial decide si ya había una activa.
        var created = await requests.CreateAsync(
            new RentalRequest
            {
                Id = requestId,
                RoomId = room.Id,
                BuildingId = building.Id,
                StudentId = studentId,
                OwnerId = room.OwnerId,
                QuickQuestions = questions,
                Message = message,
                Status = RentalRequestStatus.Pending,
                ConversationId = conversationId,
                CreatedAt = now,
                UpdatedAt = now,
            },
            cancellationToken) ?? throw new ConflictException("active_request_exists");

        try
        {
            await conversations.CreateAsync(
                new Conversation
                {
                    Id = conversationId,
                    RequestId = requestId,
                    RoomId = room.Id,
                    StudentId = studentId,
                    OwnerId = room.OwnerId,
                    Status = ConversationStatus.Open,
                    LastMessageAt = now,
                    CreatedAt = now,
                },
                cancellationToken);
        }
        catch
        {
            // Sin conversación no queda solicitud a medias.
            await requests.DeleteAsync(requestId, CancellationToken.None);
            throw;
        }

        return await ToResponseAsync(created, forLandlord: false, cancellationToken);
    }

    public async Task<RentalRequestListResponse> ListMineAsync(string studentId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var parsed = MessagingRules.ParseStatus(status);
        var (items, total) = await requests.ListByStudentAsync(studentId, parsed, page, pageSize, cancellationToken);
        return await ToListAsync(items, total, page, pageSize, forLandlord: false, cancellationToken);
    }

    public async Task<RentalRequestResponse> GetMineAsync(string studentId, string id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request.StudentId != studentId)
        {
            throw new NotFoundException("request");
        }

        return await ToResponseAsync(request, forLandlord: false, cancellationToken);
    }

    public async Task<RentalRequestResponse> CancelAsync(string studentId, string id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request.StudentId != studentId)
        {
            throw new NotFoundException("request");
        }

        if (!request.IsActive)
        {
            throw new ConflictException("request_state");
        }

        var updated = request with { Status = RentalRequestStatus.Cancelled, UpdatedAt = timeProvider.GetUtcNow() };
        if (!await requests.UpdateStatusAsync(updated, request.Status, cancellationToken))
        {
            throw new ConflictException("request_state");
        }

        await conversations.CloseAsync(request.ConversationId, cancellationToken);
        return await ToResponseAsync(updated, forLandlord: false, cancellationToken);
    }

    public async Task<RentalRequestListResponse> ListReceivedAsync(
        string ownerId,
        string? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var parsed = MessagingRules.ParseStatus(status);
        if (roomId is not null)
        {
            MessagingRules.EnsureId(roomId, "roomId");
        }

        var (items, total) = await requests.ListByOwnerAsync(ownerId, parsed, roomId, page, pageSize, cancellationToken);
        return await ToListAsync(items, total, page, pageSize, forLandlord: true, cancellationToken);
    }

    public async Task<RentalRequestResponse> GetReceivedAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var request = await LoadOwnedAsync(ownerId, id, cancellationToken);
        return await ToResponseAsync(request, forLandlord: true, cancellationToken);
    }

    public async Task<RentalRequestResponse> AcceptAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var request = await LoadOwnedAsync(ownerId, id, cancellationToken);
        if (request.Status != RentalRequestStatus.Pending)
        {
            throw new ConflictException("request_state");
        }

        if (!await IsRoomPublishedAsync(request.RoomId, cancellationToken))
        {
            throw new ConflictException("room_unavailable");
        }

        var updated = request with { Status = RentalRequestStatus.Accepted, UpdatedAt = timeProvider.GetUtcNow() };
        if (!await requests.UpdateStatusAsync(updated, RentalRequestStatus.Pending, cancellationToken))
        {
            throw new ConflictException("request_state");
        }

        return await ToResponseAsync(updated, forLandlord: true, cancellationToken);
    }

    public async Task<RentalRequestResponse> RejectAsync(string ownerId, string id, RejectRentalRequest body, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        if (body.Reason is null || !MessagingRules.RejectionReasons.Contains(body.Reason))
        {
            throw new RequestValidationException("reason", "reason must be not_available, profile_mismatch or other.");
        }

        var note = body.Note?.Trim();
        if (string.IsNullOrEmpty(note))
        {
            note = null;
        }

        if (note is { Length: > MessagingRules.MaximumRejectionNote })
        {
            throw new RequestValidationException("note", "note must have at most 300 characters.");
        }

        if (body.Reason == "other" && note is null)
        {
            throw new RequestValidationException("note", "note is required when reason is other.");
        }

        var request = await LoadOwnedAsync(ownerId, id, cancellationToken);
        if (request.Status != RentalRequestStatus.Pending)
        {
            throw new ConflictException("request_state");
        }

        var updated = request with
        {
            Status = RentalRequestStatus.Rejected,
            Rejection = new RequestRejection(body.Reason, note),
            UpdatedAt = timeProvider.GetUtcNow(),
        };
        if (!await requests.UpdateStatusAsync(updated, RentalRequestStatus.Pending, cancellationToken))
        {
            throw new ConflictException("request_state");
        }

        await conversations.CloseAsync(request.ConversationId, cancellationToken);
        return await ToResponseAsync(updated, forLandlord: true, cancellationToken);
    }

    private async Task EnsureNotSuspendedAsync(string first, string second, CancellationToken cancellationToken)
    {
        if (await suspensions.IsSuspendedAsync(first, cancellationToken) || await suspensions.IsSuspendedAsync(second, cancellationToken))
        {
            throw new ConflictException("participant_suspended");
        }
    }

    private async Task RequireLandlordAsync(string userId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(userId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }
    }

    private async Task<RentalRequest> LoadAsync(string id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        return await requests.GetAsync(id, cancellationToken) ?? throw new NotFoundException("request");
    }

    private async Task<RentalRequest> LoadOwnedAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var request = await LoadAsync(id, cancellationToken);
        return request.OwnerId == ownerId ? request : throw new NotFoundException("request");
    }

    private async Task<bool> IsRoomPublishedAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(roomId, cancellationToken);
        if (room is null || room.Status != RoomStatus.Available)
        {
            return false;
        }

        var building = await buildings.GetAsync(room.BuildingId, cancellationToken);
        return building?.Status == BuildingStatus.Verified;
    }

    private async Task<RentalRequestListResponse> ToListAsync(
        IReadOnlyList<RentalRequest> items,
        long total,
        int page,
        int pageSize,
        bool forLandlord,
        CancellationToken cancellationToken)
    {
        var responses = new List<RentalRequestResponse>(items.Count);
        foreach (var item in items)
        {
            responses.Add(await ToResponseAsync(item, forLandlord, cancellationToken));
        }

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new RentalRequestListResponse(responses, page, pageSize, total, totalPages);
    }

    private async Task<RentalRequestResponse> ToResponseAsync(RentalRequest request, bool forLandlord, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(request.RoomId, cancellationToken);
        var building = await buildings.GetAsync(request.BuildingId, cancellationToken);
        var landlord = await landlords.GetAsync(request.OwnerId, cancellationToken);
        var available = room?.Status == RoomStatus.Available && building?.Status == BuildingStatus.Verified;
        var title = building is null || room is null ? "Cuarto" : $"{building.Name} · Cuarto {room.Number}";

        RequestStudentSummary? student = null;
        if (forLandlord)
        {
            var profile = await profiles.GetAsync(request.StudentId, cancellationToken);
            string? university = null;
            if (profile?.UniversityId is { } universityId &&
                await universities.GetByIdAsync(universityId, cancellationToken) is { } found)
            {
                university = found.Name;
            }

            student = new RequestStudentSummary(string.IsNullOrWhiteSpace(profile?.Name) ? null : profile!.Name, university);
        }

        var now = timeProvider.GetUtcNow();
        var visit = request.IsActive
            ? (await visits.ListActiveAsync(request.StudentId, now, cancellationToken))
                .Where(item => item.RoomId == request.RoomId)
                .OrderBy(item => item.ScheduledAt)
                .FirstOrDefault()
            : null;

        return new RentalRequestResponse(
            request.Id,
            MessagingRules.Wire(request.Status),
            new RequestRoomSummary(request.RoomId, request.BuildingId, title, building?.Address.City ?? string.Empty),
            request.QuickQuestions,
            request.Message,
            student,
            new RequestLandlordSummary(MessagingRules.FirstName(landlord?.FullName)),
            visit is null ? null : new RequestVisitSummary(visit.Id, visit.ScheduledAt, visit.Status.ToString().ToLowerInvariant()),
            request.ConversationId,
            request.Rejection is null ? null : new RequestRejectionResponse(request.Rejection.Reason, request.Rejection.Note),
            available,
            request.CreatedAt,
            request.UpdatedAt);
    }
}

// NV-16: chat por consulta incremental.
public sealed class ConversationService(
    IConversationRepository conversations,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    ILandlordProfileRepository landlords,
    IProfileRepository profiles,
    IAccountSuspensionQuery suspensions,
    TimeProvider timeProvider)
{
    public async Task<ConversationListResponse> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (items, total) = await conversations.ListForUserAsync(userId, page, pageSize, cancellationToken);
        var responses = new List<ConversationResponse>(items.Count);
        foreach (var conversation in items)
        {
            responses.Add(await ToResponseAsync(userId, conversation, cancellationToken));
        }

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new ConversationListResponse(responses, page, pageSize, total, totalPages);
    }

    // Etiqueta para la consulta incremental: cambia solo cuando llega un mensaje.
    public async Task<(Conversation Conversation, string ETag)> GetForParticipantAsync(string userId, string id, CancellationToken cancellationToken)
    {
        var conversation = await LoadAsync(userId, id, cancellationToken);
        return (conversation, ETagFor(conversation));
    }

    public static string ETagFor(Conversation conversation) => $"\"m-{conversation.LastMessageId ?? "0"}\"";

    public async Task<MessageListResponse> ListMessagesAsync(
        string userId,
        Conversation conversation,
        string? after,
        int limit,
        CancellationToken cancellationToken)
    {
        if (after is not null)
        {
            MessagingRules.EnsureId(after, "after");
        }

        if (limit is < 1 or > MessagingRules.MaximumMessagesPerPage)
        {
            throw new RequestValidationException("limit", "limit must be between 1 and 50.");
        }

        var items = await conversations.ListAfterAsync(conversation.Id, after, limit + 1, cancellationToken);
        var page = items.Take(limit).Select(message => ToResponse(userId, message)).ToArray();
        return new MessageListResponse(page, items.Count > limit, MessagingRules.PollAfterSeconds);
    }

    public async Task<(MessageResponse Message, bool Created)> SendAsync(
        string userId,
        string id,
        SendMessageRequest body,
        CancellationToken cancellationToken)
    {
        var text = body.Text;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MessagingRules.MaximumMessageText)
        {
            throw new RequestValidationException("text", "text must have between 1 and 1000 characters.");
        }

        if (!Guid.TryParseExact(body.ClientMessageId, "D", out var clientId))
        {
            throw new RequestValidationException("clientMessageId", "clientMessageId must be a UUID.");
        }

        var conversation = await LoadAsync(userId, id, cancellationToken);
        if (conversation.Status != ConversationStatus.Open)
        {
            throw new ConflictException("conversation_closed");
        }

        if (await suspensions.IsSuspendedAsync(conversation.StudentId, cancellationToken) ||
            await suspensions.IsSuspendedAsync(conversation.OwnerId, cancellationToken))
        {
            throw new ConflictException("participant_suspended");
        }

        var (message, created) = await conversations.AppendAsync(
            new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderId = userId,
                SenderRole = userId == conversation.StudentId ? MessageRoles.Student : MessageRoles.Landlord,
                Text = text,
                ClientMessageId = clientId.ToString("D"),
                CreatedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken);
        return (ToResponse(userId, message), created);
    }

    public async Task MarkReadAsync(string userId, string id, MarkReadRequest body, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(body.LastMessageId, "lastMessageId");
        var conversation = await LoadAsync(userId, id, cancellationToken);
        if (await conversations.GetMessageAsync(conversation.Id, body.LastMessageId!, cancellationToken) is null)
        {
            throw new NotFoundException("message");
        }

        await conversations.MarkReadAsync(conversation.Id, userId == conversation.StudentId, body.LastMessageId!, cancellationToken);
    }

    private async Task<Conversation> LoadAsync(string userId, string id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        var conversation = await conversations.GetAsync(id, cancellationToken);
        return conversation is not null && conversation.IsParticipant(userId)
            ? conversation
            : throw new NotFoundException("conversation");
    }

    private async Task<ConversationResponse> ToResponseAsync(string userId, Conversation conversation, CancellationToken cancellationToken)
    {
        var isStudent = userId == conversation.StudentId;
        var room = await rooms.GetAsync(conversation.RoomId, cancellationToken);
        var building = room is null ? null : await buildings.GetAsync(room.BuildingId, cancellationToken);
        var title = building is null || room is null ? "Cuarto" : $"{building.Name} · Cuarto {room.Number}";

        string name;
        if (isStudent)
        {
            name = MessagingRules.FirstName((await landlords.GetAsync(conversation.OwnerId, cancellationToken))?.FullName);
        }
        else
        {
            var profile = await profiles.GetAsync(conversation.StudentId, cancellationToken);
            name = string.IsNullOrWhiteSpace(profile?.Name) ? "Estudiante" : profile!.Name!;
        }

        var unread = await conversations.CountUnreadAsync(
            conversation.Id,
            userId,
            isStudent ? conversation.StudentLastReadId : conversation.OwnerLastReadId,
            MessagingRules.UnreadCap,
            cancellationToken);

        return new ConversationResponse(
            conversation.Id,
            conversation.RequestId,
            new ConversationRoomSummary(conversation.RoomId, title),
            new ConversationCounterpart(name, isStudent ? MessageRoles.Landlord : MessageRoles.Student),
            conversation.Status.ToString().ToLowerInvariant(),
            conversation.LastMessageAt,
            unread);
    }

    private static MessageResponse ToResponse(string userId, ChatMessage message) =>
        new(message.Id, message.SenderRole, message.Text, message.CreatedAt, message.SenderId == userId);
}
