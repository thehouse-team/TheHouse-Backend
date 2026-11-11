using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Messaging;

// NV-16 §6.1: solicitudes de interés y chat. Las entradas rechazan campos
// desconocidos (asignación masiva).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateRentalRequest
{
    public IReadOnlyList<string>? QuickQuestions { get; init; }

    public string? Message { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RejectRentalRequest
{
    public string? Reason { get; init; }

    public string? Note { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SendMessageRequest
{
    public string? Text { get; init; }

    public string? ClientMessageId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MarkReadRequest
{
    public string? LastMessageId { get; init; }
}

public sealed record RequestRoomSummary(string Id, string BuildingId, string Title, string City);

// Solo la ve el arrendador: nombre del perfil (NV-11) y universidad.
public sealed record RequestStudentSummary(string? Name, string? University);

public sealed record RequestLandlordSummary(string Name);

public sealed record RequestVisitSummary(string Id, DateTimeOffset ScheduledAt, string Status);

public sealed record RequestRejectionResponse(string Reason, string? Note);

public sealed record RentalRequestResponse(
    string Id,
    string Status,
    RequestRoomSummary Room,
    IReadOnlyList<string> QuickQuestions,
    string? Message,
    RequestStudentSummary? Student,
    RequestLandlordSummary Landlord,
    RequestVisitSummary? Visit,
    string ConversationId,
    RequestRejectionResponse? Rejection,
    bool RoomAvailable,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RentalRequestListResponse(
    IReadOnlyList<RentalRequestResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record ConversationRoomSummary(string Id, string Title);

public sealed record ConversationCounterpart(string Name, string Role);

public sealed record ConversationResponse(
    string Id,
    string RequestId,
    ConversationRoomSummary Room,
    ConversationCounterpart Counterpart,
    string Status,
    DateTimeOffset LastMessageAt,
    int UnreadCount);

public sealed record ConversationListResponse(
    IReadOnlyList<ConversationResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record MessageResponse(
    string Id,
    string SenderRole,
    string Text,
    DateTimeOffset CreatedAt,
    bool Mine);

public sealed record MessageListResponse(
    IReadOnlyList<MessageResponse> Items,
    bool HasMore,
    int PollAfterSeconds);
