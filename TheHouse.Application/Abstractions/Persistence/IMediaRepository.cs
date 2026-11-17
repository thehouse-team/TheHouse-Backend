using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-13: metadatos (`media_items`, `verification_documents`) y bytes en
// GridFS (`media` y `documents`).
public interface IMediaRepository
{
    Task<MediaPhoto> AddPhotoAsync(MediaPhoto photo, byte[] full, byte[] thumb, CancellationToken cancellationToken);

    Task<MediaPhoto?> GetPhotoAsync(string id, CancellationToken cancellationToken);

    Task<byte[]?> ReadPhotoAsync(string id, bool thumb, CancellationToken cancellationToken);

    // Fotos de esos edificios (incluidas las de sus cuartos); orden createdAt asc.
    Task<IReadOnlyList<MediaPhoto>> ListPhotosByBuildingsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken);

    Task DeletePhotoAsync(string id, CancellationToken cancellationToken);

    // Bytes de fotos y documentos del arrendador (cuota).
    Task<long> UsageAsync(string ownerId, CancellationToken cancellationToken);

    // Reemplaza el documento del mismo edificio y `kind`.
    Task<VerificationDocument> SaveDocumentAsync(VerificationDocument document, byte[] content, CancellationToken cancellationToken);

    Task<IReadOnlyList<VerificationDocument>> ListDocumentsAsync(string buildingId, CancellationToken cancellationToken);

    Task<VerificationDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken);

    Task<byte[]?> ReadDocumentAsync(string id, CancellationToken cancellationToken);

    Task SetRetentionAsync(string buildingId, DateTimeOffset until, CancellationToken cancellationToken);

    Task<int> DeleteExpiredDocumentsAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken);

    Task DeleteByRoomAsync(string roomId, CancellationToken cancellationToken);
}
