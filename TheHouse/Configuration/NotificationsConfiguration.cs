using MongoDB.Driver;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-20: bandeja y puerto de notificaciones. El puerto registrado nunca lanza:
// si guardar la notificación falla, se registra y la acción principal sigue.
public static class NotificationsConfiguration
{
    public static void AddNotifications(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<NotificationPublisher>();
        builder.Services.AddSingleton<INotificationSink>(provider => new SafeNotificationSink(
            provider.GetRequiredService<NotificationPublisher>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("TheHouse.Notifications")));

        builder.Services.AddSingleton<INotificationRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableNotificationRepository()
                : new MongoNotificationRepository(database);
        });
    }
}

// Atrapa cualquier falla al notificar y la registra solo con el tipo (sin
// datos personales ni ids de usuario).
public sealed class SafeNotificationSink(INotificationSink inner, ILogger logger) : INotificationSink
{
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await inner.NotifyAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Notification {NotificationType} was not saved ({ErrorType}).", notification.Type, exception.GetType().Name);
        }
    }

    public async Task ClearAsync(string userId, string dedupeKey, CancellationToken cancellationToken)
    {
        try
        {
            await inner.ClearAsync(userId, dedupeKey, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Notifications were not marked as read ({ErrorType}).", exception.GetType().Name);
        }
    }
}
