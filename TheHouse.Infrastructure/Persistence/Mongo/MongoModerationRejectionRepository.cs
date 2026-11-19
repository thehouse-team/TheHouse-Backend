using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-21 1.2.0: documento de `moderation_rejections` (sin el contenido).
public sealed class ModerationRejectionDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("field")]
    public string Field { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    [BsonElement("maxSeverity")]
    public int MaxSeverity { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoModerationRejectionRepository(IMongoDatabase database) : IModerationRejectionRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<ModerationRejectionDocument> Collection => database.GetCollection<ModerationRejectionDocument>("moderation_rejections");

    public Task AddAsync(ModerationRejection rejection, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            await Collection.InsertOneAsync(
                new ModerationRejectionDocument
                {
                    Id = ObjectId.Parse(rejection.Id),
                    UserId = rejection.UserId,
                    Kind = rejection.Kind,
                    Field = rejection.Field,
                    Source = rejection.Source,
                    Categories = [.. rejection.Categories],
                    MaxSeverity = rejection.MaxSeverity,
                    CreatedAt = MongoDates.ToUtc(rejection.CreatedAt),
                },
                cancellationToken: cancellationToken);
        });

    public Task<(IReadOnlyList<ModerationRejection> Items, long TotalItems)> ListAsync(
        string? userId,
        string? kind,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var builder = Builders<ModerationRejectionDocument>.Filter;
            var filter = builder.Empty;
            if (userId is not null)
            {
                filter &= builder.Eq(item => item.UserId, userId);
            }

            if (kind is not null)
            {
                filter &= builder.Eq(item => item.Kind, kind);
            }

            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<ModerationRejection>)documents.Select(document => new ModerationRejection(
                document.Id.ToString(),
                document.UserId,
                document.Kind,
                document.Field,
                document.Source,
                document.Categories,
                document.MaxSeverity,
                MongoDates.FromUtc(document.CreatedAt))).ToList(), total);
        });

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
                var keys = Builders<ModerationRejectionDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<ModerationRejectionDocument>(
                            keys.Ascending(item => item.UserId).Descending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ix_moderation_rejections_user_created" }),
                        new CreateIndexModel<ModerationRejectionDocument>(
                            keys.Ascending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ttl_moderation_rejections_created", ExpireAfter = ContentModerationRules.Retention }),
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

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableModerationRejectionRepository : IModerationRejectionRepository
{
    public Task AddAsync(ModerationRejection rejection, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<ModerationRejection> Items, long TotalItems)> ListAsync(string? userId, string? kind, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();
}
