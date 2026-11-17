using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.TestDoubles;

// NV-13: doble en memoria de fotos y documentos (metadatos + bytes) con la
// semántica del repositorio GridFS: un documento por edificio y `kind`.
public sealed class InMemoryMediaRepository : IMediaRepository
{
    private readonly object gate = new();

    public Dictionary<string, MediaPhoto> Photos { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, (byte[] Full, byte[] Thumb)> PhotoBytes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, VerificationDocument> Documents { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, byte[]> DocumentBytes { get; } = new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<MediaPhoto> AddPhotoAsync(MediaPhoto photo, byte[] full, byte[] thumb, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Photos[photo.Id] = photo;
            PhotoBytes[photo.Id] = (full, thumb);
        }

        return Task.FromResult(photo);
    }

    public Task<MediaPhoto?> GetPhotoAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Photos.GetValueOrDefault(id));
        }
    }

    public Task<byte[]?> ReadPhotoAsync(string id, bool thumb, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<byte[]?>(PhotoBytes.TryGetValue(id, out var bytes) ? (thumb ? bytes.Thumb : bytes.Full) : null);
        }
    }

    public Task<IReadOnlyList<MediaPhoto>> ListPhotosByBuildingsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<MediaPhoto>>(Photos.Values
                .Where(photo => buildingIds.Contains(photo.BuildingId))
                .OrderBy(photo => photo.CreatedAt).ThenBy(photo => photo.Id, StringComparer.Ordinal)
                .ToArray());
        }
    }

    public Task DeletePhotoAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            Photos.Remove(id);
            PhotoBytes.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task<long> UsageAsync(string ownerId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(
                Photos.Values.Where(photo => photo.OwnerId == ownerId).Sum(photo => photo.Bytes) +
                Documents.Values.Where(document => document.OwnerId == ownerId).Sum(document => document.Bytes));
        }
    }

    public Task<VerificationDocument> SaveDocumentAsync(VerificationDocument document, byte[] content, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var previous in Documents.Values.Where(item => item.BuildingId == document.BuildingId && item.Kind == document.Kind).ToArray())
            {
                Documents.Remove(previous.Id);
                DocumentBytes.Remove(previous.Id);
            }

            Documents[document.Id] = document;
            DocumentBytes[document.Id] = content;
        }

        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<VerificationDocument>> ListDocumentsAsync(string buildingId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<VerificationDocument>>(Documents.Values.Where(item => item.BuildingId == buildingId).ToArray());
        }
    }

    public Task<VerificationDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(Documents.GetValueOrDefault(id));
        }
    }

    public Task<byte[]?> ReadDocumentAsync(string id, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            return Task.FromResult(DocumentBytes.GetValueOrDefault(id));
        }
    }

    public Task SetRetentionAsync(string buildingId, DateTimeOffset until, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var document in Documents.Values.Where(item => item.BuildingId == buildingId).ToArray())
            {
                Documents[document.Id] = document with { RetentionUntil = until };
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> DeleteExpiredDocumentsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            var expired = Documents.Values.Where(item => item.RetentionUntil is { } until && until <= now).ToArray();
            foreach (var document in expired)
            {
                Documents.Remove(document.Id);
                DocumentBytes.Remove(document.Id);
            }

            return Task.FromResult(expired.Length);
        }
    }

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var photo in Photos.Values.Where(item => item.BuildingId == buildingId).ToArray())
            {
                Photos.Remove(photo.Id);
                PhotoBytes.Remove(photo.Id);
            }

            foreach (var document in Documents.Values.Where(item => item.BuildingId == buildingId).ToArray())
            {
                Documents.Remove(document.Id);
                DocumentBytes.Remove(document.Id);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteByRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        Guard(cancellationToken);
        lock (gate)
        {
            foreach (var photo in Photos.Values.Where(item => item.RoomId == roomId).ToArray())
            {
                Photos.Remove(photo.Id);
                PhotoBytes.Remove(photo.Id);
            }
        }

        return Task.CompletedTask;
    }

    private void Guard(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    }
}

// NV-13: procesador falso para unitarias (no depende de SkiaSharp): acepta
// cualquier imagen con firma válida y devuelve bytes fijos.
public sealed class FakeImageProcessor : IImageProcessor
{
    public bool RejectAsTooLarge { get; set; }

    public ProcessedImage Process(ReadOnlyMemory<byte> input) =>
        RejectAsTooLarge
            ? throw new MediaRejectedException("image_too_large")
            : new ProcessedImage(new byte[1000], new byte[100], 1600, 1200);
}
