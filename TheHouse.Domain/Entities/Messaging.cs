namespace TheHouse.Domain.Entities;

// NV-16: solicitud de interés de un estudiante por un cuarto publicado.
public enum RentalRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled,
}

public sealed record RequestRejection(string Reason, string? Note);

public sealed record RentalRequest
{
    public string Id { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string StudentId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public IReadOnlyList<string> QuickQuestions { get; init; } = Array.Empty<string>();

    public string? Message { get; init; }

    public RentalRequestStatus Status { get; init; } = RentalRequestStatus.Pending;

    public RequestRejection? Rejection { get; init; }

    public string ConversationId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    // Activa = cuenta para el límite y para el índice único estudiante–cuarto.
    public bool IsActive => Status is RentalRequestStatus.Pending or RentalRequestStatus.Accepted;
}

public enum ConversationStatus
{
    Open,
    Closed,
}

// Una conversación por solicitud entre el estudiante y el dueño del cuarto.
public sealed record Conversation
{
    public string Id { get; init; } = string.Empty;

    public string RequestId { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string StudentId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public ConversationStatus Status { get; init; } = ConversationStatus.Open;

    public DateTimeOffset LastMessageAt { get; init; }

    public string? LastMessageId { get; init; }

    public string? StudentLastReadId { get; init; }

    public string? OwnerLastReadId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsParticipant(string userId) => userId == StudentId || userId == OwnerId;

    public string CounterpartOf(string userId) => userId == StudentId ? OwnerId : StudentId;
}

public static class MessageRoles
{
    public const string Student = "student";

    public const string Landlord = "landlord";
}

// Mensajes inmutables en texto plano.
public sealed record ChatMessage
{
    public string Id { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public string SenderId { get; init; } = string.Empty;

    public string SenderRole { get; init; } = MessageRoles.Student;

    public string Text { get; init; } = string.Empty;

    public string ClientMessageId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
