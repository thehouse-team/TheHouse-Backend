using System.Globalization;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

// NV-20 1.3.0 (§9.1): aviso `favorite_unavailable` cuando un cuarto deja de
// estar publicado (rentado, pausado, eliminado o con su edificio suspendido).
// Usa solo los puertos de Favorites y Notifications, así que los módulos que
// lo llaman (Rentals, Listings y Moderation) no dependen de otro servicio.
// Mejor esfuerzo: si los favoritos no responden, la acción principal sigue.
public static class FavoriteNotices
{
    public const int MaximumRecipients = 200;

    public static async Task NotifyUnavailableAsync(
        IFavoriteRepository? favorites,
        INotificationSink? notifications,
        string roomId,
        IReadOnlyCollection<string> excluded,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (favorites is null || notifications is null)
        {
            return;
        }

        IReadOnlyList<string> users;
        try
        {
            users = await favorites.ListUserIdsByRoomAsync(roomId, MaximumRecipients + excluded.Count, cancellationToken);
        }
        catch (DependencyUnavailableException)
        {
            return;
        }

        var day = TimeZoneInfo.ConvertTime(now, VisitSchedulePolicy.MexicoCity).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var userId in users.Where(user => !excluded.Contains(user)).Distinct(StringComparer.Ordinal).Take(MaximumRecipients))
        {
            await notifications.NotifyAsync(
                new NotificationEvent(
                    NotificationTypes.FavoriteUnavailable,
                    userId,
                    roomId,
                    $"{NotificationTypes.FavoriteUnavailable}:{roomId}:{userId}:{day}",
                    RoomId: roomId,
                    Once: true),
                cancellationToken);
        }
    }
}
