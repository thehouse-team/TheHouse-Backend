using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-20: textos fijos por tipo. Solo nombre sin apellidos, título del cuarto o
// nombre del edificio y fecha en Ciudad de México; nunca dirección, teléfono,
// correo, texto del chat ni notas (RNF-02).
public static class NotificationTemplates
{
    private static readonly string[] Months =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    public static (string Title, string Body) Render(string type, string actor, string place, DateTimeOffset? at)
    {
        var date = at is { } value ? Date(value) : string.Empty;
        var time = at is { } moment ? Time(moment) : string.Empty;
        return type switch
        {
            NotificationTypes.RequestReceived => ("Nueva solicitud", $"{actor} pidió informes de {place}."),
            NotificationTypes.RequestCancelled => ("Solicitud cancelada", $"{actor} canceló su solicitud de {place}."),
            NotificationTypes.RequestAccepted => ("Solicitud aceptada", $"{actor} aceptó tu solicitud de {place}."),
            NotificationTypes.RequestRejected => ("Solicitud no aceptada", $"{actor} no aceptó tu solicitud de {place}."),
            NotificationTypes.MessageReceived => ("Mensaje nuevo", $"{actor} te escribió sobre {place}."),
            NotificationTypes.VisitRequested => ("Nueva visita solicitada", $"{actor} quiere visitar {place} el {date} a las {time}."),
            NotificationTypes.VisitConfirmed => ("Visita confirmada", $"{actor} confirmó tu visita a {place} el {date} a las {time}."),
            NotificationTypes.VisitRescheduled => ("Visita reprogramada", $"{actor} movió tu visita a {place} al {date} a las {time}."),
            NotificationTypes.VisitCancelled => ("Visita cancelada", $"{actor} canceló la visita a {place} del {date}."),
            NotificationTypes.AgreementActivated => ("Convenio aceptado", $"{actor} aceptó el convenio de {place}; el cuarto quedó rentado."),
            NotificationTypes.AgreementEnded => ("Renta terminada", $"{actor} marcó como terminada tu renta de {place}."),
            NotificationTypes.BuildingApproved => ("Edificio aprobado", $"{place} ya está publicado."),
            NotificationTypes.BuildingChangesRequested => ("Edificio con cambios pedidos", $"Revisa los cambios que se pidieron para {place}."),
            NotificationTypes.BuildingRejected => ("Edificio no aprobado", $"{place} no fue aprobado."),
            NotificationTypes.BuildingSuspended => ("Edificio suspendido", $"{place} fue suspendido y no aparece en la búsqueda."),
            NotificationTypes.BuildingUnsuspended => ("Edificio reactivado", $"{place} vuelve a estar publicado."),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown notification type."),
        };
    }

    private static DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, VisitSchedulePolicy.MexicoCity);

    private static string Date(DateTimeOffset value)
    {
        var local = Local(value);
        return $"{local.Day} de {Months[local.Month - 1]}";
    }

    private static string Time(DateTimeOffset value) => Local(value).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

// NV-20: arma y guarda la notificación del catálogo. Lanza si MongoDB falla;
// el adaptador registrado como `INotificationSink` lo atrapa y lo registra.
public sealed class NotificationPublisher(
    INotificationRepository notifications,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    ILandlordProfileRepository landlords,
    IProfileRepository profiles,
    TimeProvider timeProvider) : INotificationSink
{
    public async Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (!NotificationTypes.Catalog.TryGetValue(notification.Type, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(notification), notification.Type, "Unknown notification type.");
        }

        if (string.IsNullOrEmpty(notification.RecipientId))
        {
            return;
        }

        var actor = await ActorAsync(notification, cancellationToken);
        var place = await PlaceAsync(notification, cancellationToken);
        var (title, body) = NotificationTemplates.Render(notification.Type, actor, place, notification.At);
        var now = timeProvider.GetUtcNow();
        await notifications.UpsertAsync(
            new Notification
            {
                Id = MessagingIds.New(now),
                UserId = notification.RecipientId,
                Type = notification.Type,
                Category = entry.Category,
                Title = title,
                Body = body,
                TargetKind = entry.TargetKind,
                TargetId = notification.TargetId,
                DedupeKey = notification.DedupeKey,
                Count = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            group: notification.Type == NotificationTypes.MessageReceived,
            cancellationToken);
    }

    public Task ClearAsync(string userId, string dedupeKey, CancellationToken cancellationToken) =>
        notifications.MarkReadByKeyAsync(userId, dedupeKey, timeProvider.GetUtcNow(), cancellationToken);

    // El estudiante aparece con el nombre de su perfil; el arrendador, con su
    // primer nombre (NV-16/NV-17).
    private async Task<string> ActorAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.StudentId is { } studentId)
        {
            var profile = await profiles.GetAsync(studentId, cancellationToken);
            return string.IsNullOrWhiteSpace(profile?.Name) ? "Un estudiante" : MessagingRules.FirstName(profile!.Name);
        }

        if (notification.LandlordId is { } landlordId)
        {
            return MessagingRules.FirstName((await landlords.GetAsync(landlordId, cancellationToken))?.FullName);
        }

        return string.Empty;
    }

    private async Task<string> PlaceAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.RoomId is { } roomId)
        {
            var room = await rooms.GetAsync(roomId, cancellationToken);
            var building = room is null ? null : await buildings.GetAsync(room.BuildingId, cancellationToken);
            return room is null || building is null ? "el cuarto" : $"{building.Name} · Cuarto {room.Number}";
        }

        if (notification.BuildingId is { } buildingId)
        {
            var building = await buildings.GetAsync(buildingId, cancellationToken);
            return building is null ? "Tu edificio" : building.Name;
        }

        return "TheHouse";
    }
}

// NV-20: bandeja, contador y marcas de leído del usuario autenticado.
public sealed class NotificationService(INotificationRepository notifications, TimeProvider timeProvider)
{
    public async Task<NotificationListResponse> ListAsync(
        string userId,
        string? unread,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        bool? onlyUnread = unread switch
        {
            null or "" => null,
            "true" => true,
            "false" => false,
            _ => throw new RequestValidationException("unread", "unread must be true or false."),
        };
        var filter = Category(category);
        var (items, total) = await notifications.ListAsync(userId, onlyUnread, filter, page, pageSize, cancellationToken);
        return new NotificationListResponse(
            items.Select(ToResponse).ToArray(),
            page,
            pageSize,
            total,
            PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<UnreadCountResponse> CountAsync(string userId, CancellationToken cancellationToken)
    {
        var counts = await notifications.CountUnreadAsync(userId, cancellationToken);
        return new UnreadCountResponse(counts.Unread, counts.Requests, counts.Visits, counts.System);
    }

    public async Task MarkReadAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        if (!await notifications.MarkReadAsync(userId, id!.ToLowerInvariant(), timeProvider.GetUtcNow(), cancellationToken))
        {
            throw new NotFoundException("notification");
        }
    }

    public Task MarkAllReadAsync(string userId, ReadAllNotificationsRequest? body, CancellationToken cancellationToken) =>
        notifications.MarkAllReadAsync(userId, Category(body?.Category), timeProvider.GetUtcNow(), cancellationToken);

    // NV-16: leer el chat marca la notificación agrupada de esa conversación.
    public static string MessageKey(string conversationId) => "message:" + conversationId;

    private static string? Category(string? value) =>
        value is null or "" ? null
        : NotificationCategories.All.Contains(value) ? value
        : throw new RequestValidationException("category", "category must be requests, visits or system.");

    private static NotificationResponse ToResponse(Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Category,
            notification.Title,
            notification.Body,
            new NotificationTarget(notification.TargetKind, notification.TargetId),
            notification.Count,
            notification.IsRead,
            notification.CreatedAt,
            notification.UpdatedAt);
}
