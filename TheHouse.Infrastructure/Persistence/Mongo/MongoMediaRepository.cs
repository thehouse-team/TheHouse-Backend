using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-13: metadatos de fotos (`media_items`) y documentos
// (`verification_documents`); bytes en los buckets GridFS `media` y
// `documents` de la misma base (M-BE-073).
public sealed class MediaItemDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    [BsonIgnoreIfNull]
    public string? RoomId { get; set; }

    [BsonElement("fileId")]
    public ObjectId FileId { get; set; }

    [BsonElement("thumbFileId")]
    public ObjectId ThumbFileId { get; set; }

    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }

    [BsonElement("bytes")]
    public long Bytes { get; set; }

    [BsonElement("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class VerificationDocumentDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("fileId")]
    public ObjectId FileId { get; set; }

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("bytes")]
    public long Bytes { get; set; }

    [BsonElement("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [BsonElement("nonce")]
    public byte[] Nonce { get; set; } = [];

    [BsonElement("wrappedKey")]
    public byte[] WrappedKey { get; set; } = [];

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    [BsonElement("retentionUntil")]
    [BsonIgnoreIfNull]
    public DateTime? RetentionUntil { get; set; }
}

public sealed class MongoMediaRepository(IMongoDatabase database) : IMediaRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<MediaItemDocument> Items => database.GetCollection<MediaItemDocument>("media_items");

    private IMongoCollection<VerificationDocumentDocument> Documents => database.GetCollection<VerificationDocumentDocument>("verification_documents");

    private GridFSBucket PhotoFiles => new(database, new GridFSBucketOptions { BucketName = "media" });

    private GridFSBucket DocumentFiles => new(database, new GridFSBucketOptions { BucketName = "documents" });

    public Task<MediaPhoto> AddPhotoAsync(MediaPhoto photo, byte[] full, byte[] thumb, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var fileId = await PhotoFiles.UploadFromBytesAsync(photo.Id, full, cancellationToken: cancellationToken);
            var thumbId = await PhotoFiles.UploadFromBytesAsync(photo.Id + "-thumb", thumb, cancellationToken: cancellationToken);
            await Items.InsertOneAsync(
                new MediaItemDocument
                {
                    Id = ObjectId.Parse(photo.Id),
                    OwnerId = photo.OwnerId,
                    BuildingId = photo.BuildingId,
                    RoomId = photo.RoomId,
                    FileId = fileId,
                    ThumbFileId = thumbId,
                    Width = photo.Width,
                    Height = photo.Height,
                    Bytes = photo.Bytes,
                    Sha256 = photo.Sha256,
                    CreatedAt = MongoDates.ToUtc(photo.CreatedAt),
                },
                cancellationToken: cancellationToken);
            return photo;
        });

    public Task<MediaPhoto?> GetPhotoAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Items.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<byte[]?> ReadPhotoAsync(string id, bool thumb, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Items.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : await PhotoFiles.DownloadAsBytesAsync(thumb ? document.ThumbFileId : document.FileId, cancellationToken: cancellationToken);
        });

    public Task<IReadOnlyList<MediaPhoto>> ListPhotosByBuildingsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var documents = await Items.Find(Builders<MediaItemDocument>.Filter.In(item => item.BuildingId, buildingIds))
                .SortBy(item => item.CreatedAt).ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<MediaPhoto>)documents.Select(Map).ToArray();
        });

    public Task DeletePhotoAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(id);
            var document = await Items.FindOneAndDeleteAsync(item => item.Id == objectId, cancellationToken: cancellationToken);
            if (document is not null)
            {
                await DeleteFileAsync(PhotoFiles, document.FileId, cancellationToken);
                await DeleteFileAsync(PhotoFiles, document.ThumbFileId, cancellationToken);
            }
        });

    public Task<long> UsageAsync(string ownerId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var photos = await Items.Aggregate().Match(item => item.OwnerId == ownerId)
                .Group(item => 1, group => new { Total = group.Sum(item => item.Bytes) })
                .FirstOrDefaultAsync(cancellationToken);
            var documents = await Documents.Aggregate().Match(item => item.OwnerId == ownerId)
                .Group(item => 1, group => new { Total = group.Sum(item => item.Bytes) })
                .FirstOrDefaultAsync(cancellationToken);
            return (photos?.Total ?? 0) + (documents?.Total ?? 0);
        });

    public Task<VerificationDocument> SaveDocumentAsync(VerificationDocument document, byte[] content, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var fileId = await DocumentFiles.UploadFromBytesAsync(document.Id, content, cancellationToken: cancellationToken);
            var previous = await Documents.FindOneAndReplaceAsync<VerificationDocumentDocument>(
                item => item.BuildingId == document.BuildingId && item.Kind == document.Kind,
                ToDocument(document, fileId),
                new FindOneAndReplaceOptions<VerificationDocumentDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.Before },
                cancellationToken);
            if (previous is not null)
            {
                await DeleteFileAsync(DocumentFiles, previous.FileId, cancellationToken);
            }

            return document;
        });

    public Task<IReadOnlyList<VerificationDocument>> ListDocumentsAsync(string buildingId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var documents = await Documents.Find(item => item.BuildingId == buildingId).ToListAsync(cancellationToken);
            return (IReadOnlyList<VerificationDocument>)documents.Select(Map).ToArray();
        });

    public Task<VerificationDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Documents.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<byte[]?> ReadDocumentAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(id);
            var document = await Documents.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : await DocumentFiles.DownloadAsBytesAsync(document.FileId, cancellationToken: cancellationToken);
        });

    public Task SetRetentionAsync(string buildingId, DateTimeOffset until, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Documents.UpdateManyAsync(
            item => item.BuildingId == buildingId,
            Builders<VerificationDocumentDocument>.Update.Set(item => item.RetentionUntil, MongoDates.ToUtc(until)),
            cancellationToken: cancellationToken));

    public Task<int> DeleteExpiredDocumentsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var limit = MongoDates.ToUtc(now);
            var expired = await Documents.Find(item => item.RetentionUntil != null && item.RetentionUntil <= limit).ToListAsync(cancellationToken);
            foreach (var document in expired)
            {
                await DeleteFileAsync(DocumentFiles, document.FileId, cancellationToken);
                await Documents.DeleteOneAsync(item => item.Id == document.Id, cancellationToken);
            }

            return expired.Count;
        });

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            foreach (var item in await Items.Find(item => item.BuildingId == buildingId).ToListAsync(cancellationToken))
            {
                await DeletePhotoAsync(item.Id.ToString(), cancellationToken);
            }

            foreach (var document in await Documents.Find(item => item.BuildingId == buildingId).ToListAsync(cancellationToken))
            {
                await DeleteFileAsync(DocumentFiles, document.FileId, cancellationToken);
                await Documents.DeleteOneAsync(item => item.Id == document.Id, cancellationToken);
            }
        });

    public Task DeleteByRoomAsync(string roomId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            foreach (var item in await Items.Find(item => item.RoomId == roomId).ToListAsync(cancellationToken))
            {
                await DeletePhotoAsync(item.Id.ToString(), cancellationToken);
            }
        });

    private static async Task DeleteFileAsync(GridFSBucket bucket, ObjectId id, CancellationToken cancellationToken)
    {
        try
        {
            await bucket.DeleteAsync(id, cancellationToken);
        }
        catch (GridFSFileNotFoundException)
        {
            // Ya no existe: el borrado es idempotente.
        }
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (ready)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!ready)
            {
                var itemKeys = Builders<MediaItemDocument>.IndexKeys;
                await Items.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<MediaItemDocument>(itemKeys.Ascending(item => item.BuildingId).Ascending(item => item.RoomId), new CreateIndexOptions { Name = "ix_media_items_building_room" }),
                        new CreateIndexModel<MediaItemDocument>(itemKeys.Ascending(item => item.OwnerId), new CreateIndexOptions { Name = "ix_media_items_owner" }),
                    ],
                    cancellationToken);
                var documentKeys = Builders<VerificationDocumentDocument>.IndexKeys;
                await Documents.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<VerificationDocumentDocument>(documentKeys.Ascending(item => item.BuildingId).Ascending(item => item.Kind), new CreateIndexOptions { Unique = true, Name = "ux_verification_documents_building_kind" }),
                        new CreateIndexModel<VerificationDocumentDocument>(documentKeys.Ascending(item => item.OwnerId), new CreateIndexOptions { Name = "ix_verification_documents_owner" }),
                        new CreateIndexModel<VerificationDocumentDocument>(documentKeys.Ascending(item => item.RetentionUntil), new CreateIndexOptions { Name = "ix_verification_documents_retention", Sparse = true }),
                    ],
                    cancellationToken);
                ready = true;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static MediaPhoto Map(MediaItemDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            OwnerId = document.OwnerId,
            BuildingId = document.BuildingId,
            RoomId = document.RoomId,
            Width = document.Width,
            Height = document.Height,
            Bytes = document.Bytes,
            Sha256 = document.Sha256,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
        };

    private static VerificationDocumentDocument ToDocument(VerificationDocument document, ObjectId fileId) =>
        new()
        {
            Id = ObjectId.Parse(document.Id),
            OwnerId = document.OwnerId,
            BuildingId = document.BuildingId,
            Kind = document.Kind,
            FileId = fileId,
            ContentType = document.ContentType,
            Bytes = document.Bytes,
            Sha256 = document.Sha256,
            Nonce = document.Nonce,
            WrappedKey = document.WrappedKey,
            UploadedAt = MongoDates.ToUtc(document.UploadedAt),
            RetentionUntil = document.RetentionUntil is { } until ? MongoDates.ToUtc(until) : null,
        };

    private static VerificationDocument Map(VerificationDocumentDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            OwnerId = document.OwnerId,
            BuildingId = document.BuildingId,
            Kind = document.Kind,
            ContentType = document.ContentType,
            Bytes = document.Bytes,
            Sha256 = document.Sha256,
            Nonce = document.Nonce,
            WrappedKey = document.WrappedKey,
            UploadedAt = MongoDates.FromUtc(document.UploadedAt),
            RetentionUntil = document.RetentionUntil is { } until ? MongoDates.FromUtc(until) : null,
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableMediaRepository : IMediaRepository
{
    public Task<MediaPhoto> AddPhotoAsync(MediaPhoto photo, byte[] full, byte[] thumb, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<MediaPhoto?> GetPhotoAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<byte[]?> ReadPhotoAsync(string id, bool thumb, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<MediaPhoto>> ListPhotosByBuildingsAsync(IReadOnlyCollection<string> buildingIds, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeletePhotoAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<long> UsageAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<VerificationDocument> SaveDocumentAsync(VerificationDocument document, byte[] content, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<VerificationDocument>> ListDocumentsAsync(string buildingId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<VerificationDocument?> GetDocumentAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<byte[]?> ReadDocumentAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SetRetentionAsync(string buildingId, DateTimeOffset until, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<int> DeleteExpiredDocumentsAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteByRoomAsync(string roomId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
