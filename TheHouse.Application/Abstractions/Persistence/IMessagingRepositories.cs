using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-16: `rental_requests` (único parcial {studentId, roomId} en activas).
public interface IRentalRequestRepository
{
    // Devuelve null si ya existe una solicitud activa del estudiante para ese cuarto.
    Task<RentalRequest?> CreateAsync(RentalRequest request, CancellationToken cancellationToken);

    Task<RentalRequest?> GetAsync(string id, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    Task<long> CountActiveByStudentAsync(string studentId, CancellationToken cancellationToken);

    // Orden: updatedAt desc, id desc.
    Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByStudentAsync(
        string studentId,
        RentalRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<RentalRequest> Items, long TotalItems)> ListByOwnerAsync(
        string ownerId,
        RentalRequestStatus? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    // Cambia el estado solo si sigue en `expected`; false si otro lo cambió antes.
    Task<bool> UpdateStatusAsync(RentalRequest updated, RentalRequestStatus expected, CancellationToken cancellationToken);
}

// NV-16: `conversations` y `messages`.
public interface IConversationRepository
{
    Task<Conversation> CreateAsync(Conversation conversation, CancellationToken cancellationToken);

    Task<Conversation?> GetAsync(string id, CancellationToken cancellationToken);

    Task CloseAsync(string id, CancellationToken cancellationToken);

    // Orden: lastMessageAt desc, id desc.
    Task<(IReadOnlyList<Conversation> Items, long TotalItems)> ListForUserAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    // Inserta el mensaje y actualiza la conversación. Si el remitente ya usó ese
    // `clientMessageId` en la conversación, devuelve el mensaje existente y false.
    Task<(ChatMessage Message, bool Created)> AppendAsync(ChatMessage message, CancellationToken cancellationToken);

    // Mensajes con id mayor que `afterId` en orden ascendente; hasta `limit` elementos.
    Task<IReadOnlyList<ChatMessage>> ListAfterAsync(
        string conversationId,
        string? afterId,
        int limit,
        CancellationToken cancellationToken);

    Task<ChatMessage?> GetMessageAsync(string conversationId, string messageId, CancellationToken cancellationToken);

    // Mensajes del otro participante posteriores a `afterId`, con tope `cap`.
    Task<int> CountUnreadAsync(
        string conversationId,
        string readerId,
        string? afterId,
        int cap,
        CancellationToken cancellationToken);

    // Guarda el último mensaje leído por el participante si es más reciente que el actual.
    Task MarkReadAsync(string conversationId, bool readerIsStudent, string messageId, CancellationToken cancellationToken);
}
