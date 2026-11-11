using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-16: dobles en memoria con la semántica de MongoDB (único parcial de
// solicitudes activas, único de clientMessageId, orden por id y conteo de no
// leídos). Se comparten entre pruebas unitarias e integración.
public sealed class InMemoryRentalRequestRepository : IRentalRequestRepository
{
    private readonly object gate = new();

    public Dictionary<string, RentalRequest> Requests { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<RentalRequest?> CreateAsync(RentalRequest request, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (request.IsActive && Requests.Values.Any(item => item.IsActive && item.StudentId == request.StudentId && item.RoomId == request.RoomId))
            {
                return Task.FromResult<RentalRequest?>(null);
            }

            var stored = request with { Id = string.IsNullOrEmpty(request.Id) ? MessagingDoubleIds.Next() : request.Id };
            Requests[stored.Id] = stored;
            return Task.FromResult<RentalRequest?>(stored);
        }
    }

    public Task<RentalRequest?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Requests.GetValueOrDefault(id));
        }
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Requests.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task<long> CountActiveByStudentAsync(string studentId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult((long)Requests.Values.Count(item => item.StudentId == studentId && item.IsActive));
        }
    }

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByStudentAsync(
        string studentId,
        RentalRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        List(item => item.StudentId == studentId && (status is null || item.Status == status), page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        RentalRequestStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        List(item => item.OwnerId == ownerId && (status is null || item.Status == status) && (roomId is null || item.RoomId == roomId), page, pageSize, cancellationToken);

    public Task<bool> UpdateStatusAsync(RentalRequest updated, RentalRequestStatus expected, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (!Requests.TryGetValue(updated.Id, out var current) || current.Status != expected)
            {
                return Task.FromResult(false);
            }

            Requests[updated.Id] = current with { Status = updated.Status, Rejection = updated.Rejection, UpdatedAt = updated.UpdatedAt };
            return Task.FromResult(true);
        }
    }

    private Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> List(
        Func<RentalRequest, bool> filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var matching = Requests.Values.Where(filter)
                .OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<RentalRequest> items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((items, (long)matching.Count));
        }
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

public sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly object gate = new();

    public Dictionary<string, Conversation> Conversations { get; } = new(StringComparer.Ordinal);

    public List<ChatMessage> Messages { get; } = [];

    public bool Unavailable { get; set; }

    // Simula una falla al crear la conversación (para probar que no queda la solicitud).
    public bool FailCreate { get; set; }

    public Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        if (FailCreate)
        {
            throw new DependencyUnavailableException();
        }

        lock (gate)
        {
            var stored = conversation with { Id = string.IsNullOrEmpty(conversation.Id) ? MessagingDoubleIds.Next() : conversation.Id };
            Conversations[stored.Id] = stored;
            return Task.FromResult(stored);
        }
    }

    public Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Conversations.GetValueOrDefault(id));
        }
    }

    public Task CloseAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            if (Conversations.TryGetValue(id, out var conversation))
            {
                Conversations[id] = conversation with { Status = ConversationStatus.Closed };
            }
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Conversation> Items, long TotalItems)> ListForUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var matching = Conversations.Values.Where(item => item.IsParticipant(userId))
                .OrderByDescending(item => item.LastMessageAt).ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<Conversation> items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((items, (long)matching.Count));
        }
    }

    public Task<(ChatMessage Message, bool Created)> AppendAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var existing = Messages.FirstOrDefault(item =>
                item.ConversationId == message.ConversationId &&
                item.SenderId == message.SenderId &&
                item.ClientMessageId == message.ClientMessageId);
            if (existing is not null)
            {
                return Task.FromResult((existing, false));
            }

            var stored = message with { Id = MessagingDoubleIds.Next() };
            Messages.Add(stored);
            var conversation = Conversations[message.ConversationId];
            if (conversation.LastMessageId is null || string.CompareOrdinal(conversation.LastMessageId, stored.Id) < 0)
            {
                Conversations[conversation.Id] = conversation with { LastMessageId = stored.Id, LastMessageAt = stored.CreatedAt };
            }

            return Task.FromResult((stored, true));
        }
    }

    public Task<IReadOnlyList<ChatMessage>> ListAfterAsync(string conversationId, string? afterId, int limit, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            IReadOnlyList<ChatMessage> items = Messages
                .Where(item => item.ConversationId == conversationId && (afterId is null || string.CompareOrdinal(item.Id, afterId) > 0))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Take(limit)
                .ToList();
            return Task.FromResult(items);
        }
    }

    public Task<ChatMessage?> GetMessageAsync(string conversationId, string messageId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Messages.FirstOrDefault(item => item.ConversationId == conversationId && item.Id == messageId));
        }
    }

    public Task<int> CountUnreadAsync(string conversationId, string readerId, string? afterId, int cap, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var count = Messages.Count(item =>
                item.ConversationId == conversationId &&
                item.SenderId != readerId &&
                (afterId is null || string.CompareOrdinal(item.Id, afterId) > 0));
            return Task.FromResult(Math.Min(count, cap));
        }
    }

    public Task MarkReadAsync(string conversationId, bool readerIsStudent, string messageId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var conversation = Conversations[conversationId];
            var current = readerIsStudent ? conversation.StudentLastReadId : conversation.OwnerLastReadId;
            if (current is null || string.CompareOrdinal(current, messageId) < 0)
            {
                Conversations[conversationId] = readerIsStudent
                    ? conversation with { StudentLastReadId = messageId }
                    : conversation with { OwnerLastReadId = messageId };
            }
        }

        return Task.CompletedTask;
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

// Ids crecientes con formato de ObjectId para conservar el orden por id.
internal static class MessagingDoubleIds
{
    private static long counter = 0x6a00_0000_0000;

    public static string Next() => Interlocked.Increment(ref counter).ToString("x24", System.Globalization.CultureInfo.InvariantCulture);
}
