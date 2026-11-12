namespace TheHouse.Application.Abstractions.Notifications;

// NV-20 (M-BE-071): los casos de uso llaman a este puerto después de guardar
// su cambio. La implementación registrada no lanza: si la notificación falla,
// la acción principal no se deshace.
public interface INotificationSink
{
    Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken);

    // Marca como leídas las notificaciones no leídas de ese hecho (por ejemplo,
    // al leer el chat de una conversación).
    Task ClearAsync(string userId, string dedupeKey, CancellationToken cancellationToken);
}

// Hecho que se notifica. `RecipientId` sale del recurso, nunca del cliente;
// los ids de actor y cuarto solo sirven para armar el texto.
public sealed record NotificationEvent(
    string Type,
    string RecipientId,
    string TargetId,
    string DedupeKey,
    string? RoomId = null,
    string? BuildingId = null,
    string? StudentId = null,
    string? LandlordId = null,
    DateTimeOffset? At = null);
