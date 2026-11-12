using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Notifications;

// NV-20 §6.1: bandeja de notificaciones. La entrada rechaza campos
// desconocidos (asignación masiva).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReadAllNotificationsRequest
{
    public string? Category { get; init; }
}

public sealed record NotificationTarget(string Kind, string Id);

public sealed record NotificationResponse(
    string Id,
    string Type,
    string Category,
    string Title,
    string Body,
    NotificationTarget Target,
    int Count,
    bool Read,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record UnreadCountResponse(int Unread, int Requests, int Visits, int System);
