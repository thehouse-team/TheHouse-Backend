using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-20: doble en memoria de `notifications` con la semántica de MongoDB
// (una no leída por usuario y `dedupeKey`, agrupación con `count`, orden por
// `updatedAt`). Se comparte entre unitarias e integración.
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly object gate = new();

    public List<Notification> Items { get; } = [];

    public bool Unavailable { get; set; }

    public Task UpsertAsync(Notification notification, bool group, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var index = Items.FindIndex(item => item.UserId == notification.UserId && item.DedupeKey == notification.DedupeKey && !item.IsRead);
            if (index < 0)
            {
                Items.Add(notification with { Count = 1 });
            }
            else if (group)
            {
                var current = Items[index];
                Items[index] = current with { Count = current.Count + 1, Title = notification.Title, Body = notification.Body, UpdatedAt = notification.UpdatedAt };
            }
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<Notification> Items, long TotalItems)> ListAsync(
        string userId,
        bool? unread,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var all = Items
                .Where(item => item.UserId == userId && (unread is null || item.IsRead != unread) && (category is null || item.Category == category))
                .OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<(IReadOnlyList<Notification>, long)>((all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), all.Length));
        }
    }

    public Task<NotificationCounts> CountUnreadAsync(string userId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            int Of(string category) => Items.Count(item => item.UserId == userId && !item.IsRead && item.Category == category);
            return Task.FromResult(new NotificationCounts(Of(NotificationCategories.Requests), Of(NotificationCategories.Visits), Of(NotificationCategories.System)));
        }
    }

    public Task<bool> MarkReadAsync(string userId, string id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var index = Items.FindIndex(item => item.Id == id && item.UserId == userId);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            Items[index] = Items[index].IsRead ? Items[index] : Items[index] with { ReadAt = now };
            return Task.FromResult(true);
        }
    }

    public Task MarkAllReadAsync(string userId, string? category, DateTimeOffset now, CancellationToken cancellationToken) =>
        MarkWhere(item => item.UserId == userId && (category is null || item.Category == category), now, cancellationToken);

    public Task MarkReadByKeyAsync(string userId, string dedupeKey, DateTimeOffset now, CancellationToken cancellationToken) =>
        MarkWhere(item => item.UserId == userId && item.DedupeKey == dedupeKey, now, cancellationToken);

    private Task MarkWhere(Func<Notification, bool> predicate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            for (var index = 0; index < Items.Count; index++)
            {
                if (!Items[index].IsRead && predicate(Items[index]))
                {
                    Items[index] = Items[index] with { ReadAt = now };
                }
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

// NV-20: puerto que solo guarda lo que recibe, para probar los emisores.
public sealed class RecordingNotificationSink : INotificationSink
{
    public List<NotificationEvent> Events { get; } = [];

    public List<(string UserId, string DedupeKey)> Cleared { get; } = [];

    public Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        lock (Events)
        {
            Events.Add(notification);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(string userId, string dedupeKey, CancellationToken cancellationToken)
    {
        lock (Cleared)
        {
            Cleared.Add((userId, dedupeKey));
        }

        return Task.CompletedTask;
    }
}
