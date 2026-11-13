namespace TheHouse.Domain.Entities;

// NV-20: categorías de la bandeja (pestañas del prototipo).
public static class NotificationCategories
{
    public const string Requests = "requests";

    public const string Visits = "visits";

    public const string System = "system";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Requests, Visits, System };
}

// NV-20: a qué pantalla lleva la notificación.
public static class NotificationTargets
{
    public const string Request = "request";

    public const string Conversation = "conversation";

    public const string Visit = "visit";

    public const string Agreement = "agreement";

    public const string Rental = "rental";

    public const string Building = "building";
}

// NV-20: catálogo cerrado de tipos con su categoría y su destino.
public static class NotificationTypes
{
    public const string RequestReceived = "request_received";

    public const string RequestCancelled = "request_cancelled";

    public const string RequestAccepted = "request_accepted";

    public const string RequestRejected = "request_rejected";

    public const string MessageReceived = "message_received";

    public const string VisitRequested = "visit_requested";

    public const string VisitConfirmed = "visit_confirmed";

    public const string VisitRescheduled = "visit_rescheduled";

    public const string VisitCancelled = "visit_cancelled";

    public const string AgreementActivated = "agreement_activated";

    public const string AgreementEnded = "agreement_ended";

    public const string BuildingApproved = "building_approved";

    public const string BuildingChangesRequested = "building_changes_requested";

    public const string BuildingRejected = "building_rejected";

    public const string BuildingSuspended = "building_suspended";

    public const string BuildingUnsuspended = "building_unsuspended";

    public static readonly IReadOnlyDictionary<string, (string Category, string TargetKind)> Catalog =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [RequestReceived] = (NotificationCategories.Requests, NotificationTargets.Request),
            [RequestCancelled] = (NotificationCategories.Requests, NotificationTargets.Request),
            [RequestAccepted] = (NotificationCategories.Requests, NotificationTargets.Request),
            [RequestRejected] = (NotificationCategories.Requests, NotificationTargets.Request),
            [MessageReceived] = (NotificationCategories.Requests, NotificationTargets.Conversation),
            [VisitRequested] = (NotificationCategories.Visits, NotificationTargets.Visit),
            [VisitConfirmed] = (NotificationCategories.Visits, NotificationTargets.Visit),
            [VisitRescheduled] = (NotificationCategories.Visits, NotificationTargets.Visit),
            [VisitCancelled] = (NotificationCategories.Visits, NotificationTargets.Visit),
            [AgreementActivated] = (NotificationCategories.Requests, NotificationTargets.Agreement),
            [AgreementEnded] = (NotificationCategories.Requests, NotificationTargets.Rental),
            [BuildingApproved] = (NotificationCategories.System, NotificationTargets.Building),
            [BuildingChangesRequested] = (NotificationCategories.System, NotificationTargets.Building),
            [BuildingRejected] = (NotificationCategories.System, NotificationTargets.Building),
            [BuildingSuspended] = (NotificationCategories.System, NotificationTargets.Building),
            [BuildingUnsuspended] = (NotificationCategories.System, NotificationTargets.Building),
        };
}

// NV-20: notificación de la bandeja. `DedupeKey` identifica el hecho; los
// mensajes de una conversación se agrupan en una no leída con `Count`.
public sealed record Notification
{
    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string DedupeKey { get; init; } = string.Empty;

    public int Count { get; init; } = 1;

    public DateTimeOffset? ReadAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsRead => ReadAt is not null;
}

public sealed record NotificationCounts(int Requests, int Visits, int System)
{
    public int Unread => Requests + Visits + System;
}
