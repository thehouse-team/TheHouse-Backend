using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-20: `notifications` (módulo Notifications). Único parcial
// `{ userId, dedupeKey }` en no leídas; TTL de 90 días.
public interface INotificationRepository
{
    // Crea la notificación si no hay una no leída con el mismo `dedupeKey`.
    // Con `group`, si ya existe suma 1 a `count` y actualiza textos y fecha.
    Task UpsertAsync(Notification notification, bool group, CancellationToken cancellationToken);

    // Orden: updatedAt desc, id desc.
    Task<(IReadOnlyList<Notification> Items, long TotalItems)> ListAsync(
        string userId,
        bool? unread,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<NotificationCounts> CountUnreadAsync(string userId, CancellationToken cancellationToken);

    // false si la notificación no existe o es de otro usuario.
    Task<bool> MarkReadAsync(string userId, string id, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkAllReadAsync(string userId, string? category, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkReadByKeyAsync(string userId, string dedupeKey, DateTimeOffset now, CancellationToken cancellationToken);
}
