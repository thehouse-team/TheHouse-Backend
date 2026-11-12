using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-20: documento de `notifications`. `unread` sostiene el índice único
// parcial `{ userId, dedupeKey }` y el contador; TTL de 90 días.
public sealed class NotificationDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("targetKind")]
    public string TargetKind { get; set; } = string.Empty;

    [BsonElement("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [BsonElement("dedupeKey")]
    public string DedupeKey { get; set; } = string.Empty;

    [BsonElement("count")]
    public int Count { get; set; }

    [BsonElement("unread")]
    public bool Unread { get; set; }

    [BsonElement("readAt")]
    [BsonIgnoreIfNull]
    public DateTime? ReadAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class MongoNotificationRepository(IMongoDatabase database) : INotificationRepository, IDisposable
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<NotificationDocument> Collection =>
        database.GetCollection<NotificationDocument>("notifications");

    public async Task UpsertAsync(Notification notification, bool group, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await EnsureIndexesAsync(cancellationToken);
                var filter = Builders<NotificationDocument>.Filter;
                var update = Builders<NotificationDocument>.Update
                    .SetOnInsert(item => item.Id, ObjectId.Parse(notification.Id))
                    .SetOnInsert(item => item.Type, notification.Type)
                    .SetOnInsert(item => item.Category, notification.Category)
                    .SetOnInsert(item => item.TargetKind, notification.TargetKind)
                    .SetOnInsert(item => item.TargetId, notification.TargetId)
                    .SetOnInsert(item => item.CreatedAt, MongoDates.ToUtc(notification.CreatedAt));
                update = group
                    ? update
                        .Inc(item => item.Count, 1)
                        .Set(item => item.Title, notification.Title)
                        .Set(item => item.Body, notification.Body)
                        .Set(item => item.UpdatedAt, MongoDates.ToUtc(notification.UpdatedAt))
                    : update
                        .SetOnInsert(item => item.Count, 1)
                        .SetOnInsert(item => item.Title, notification.Title)
                        .SetOnInsert(item => item.Body, notification.Body)
                        .SetOnInsert(item => item.UpdatedAt, MongoDates.ToUtc(notification.UpdatedAt));
                await Collection.UpdateOneAsync(
                    filter.Eq(item => item.UserId, notification.UserId) &
                    filter.Eq(item => item.DedupeKey, notification.DedupeKey) &
                    filter.Eq(item => item.Unread, true),
                    update,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken);
                return;
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey && attempt == 0)
            {
                // Otra réplica insertó la misma notificación a la vez: se repite
                // una vez y ahora actualiza la existente.
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                throw MongoFailure.Unavailable();
            }
        }
    }

    public Task<(IReadOnlyList<Notification> Items, long TotalItems)> ListAsync(
        string userId,
        bool? unread,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var builder = Builders<NotificationDocument>.Filter;
            var filter = builder.Eq(item => item.UserId, userId);
            if (unread is { } value)
            {
                filter &= builder.Eq(item => item.Unread, value);
            }

            if (category is not null)
            {
                filter &= builder.Eq(item => item.Category, category);
            }

            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Notification>)documents.Select(Map).ToArray(), total);
        });

    public Task<NotificationCounts> CountUnreadAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var groups = await Collection.Aggregate()
                .Match(item => item.UserId == userId && item.Unread)
                .Group(item => item.Category, grouping => new { Category = grouping.Key, Count = grouping.Count() })
                .ToListAsync(cancellationToken);
            int Of(string category) => groups.FirstOrDefault(item => item.Category == category)?.Count ?? 0;
            return new NotificationCounts(Of(NotificationCategories.Requests), Of(NotificationCategories.Visits), Of(NotificationCategories.System));
        });

    public Task<bool> MarkReadAsync(string userId, string id, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return false;
            }

            var result = await Collection.UpdateOneAsync(
                item => item.Id == objectId && item.UserId == userId && item.Unread,
                Read(now),
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1 ||
                await Collection.CountDocumentsAsync(item => item.Id == objectId && item.UserId == userId, cancellationToken: cancellationToken) > 0;
        });

    public Task MarkAllReadAsync(string userId, string? category, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(() =>
        {
            var builder = Builders<NotificationDocument>.Filter;
            var filter = builder.Eq(item => item.UserId, userId) & builder.Eq(item => item.Unread, true);
            if (category is not null)
            {
                filter &= builder.Eq(item => item.Category, category);
            }

            return Collection.UpdateManyAsync(filter, Read(now), cancellationToken: cancellationToken);
        });

    public Task MarkReadByKeyAsync(string userId, string dedupeKey, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.UpdateManyAsync(
            item => item.UserId == userId && item.DedupeKey == dedupeKey && item.Unread,
            Read(now),
            cancellationToken: cancellationToken));

    private static UpdateDefinition<NotificationDocument> Read(DateTimeOffset now) =>
        Builders<NotificationDocument>.Update
            .Set(item => item.Unread, false)
            .Set(item => item.ReadAt, MongoDates.ToUtc(now));

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
                var keys = Builders<NotificationDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<NotificationDocument>(
                            keys.Ascending(item => item.UserId).Ascending(item => item.DedupeKey),
                            new CreateIndexOptions<NotificationDocument>
                            {
                                Unique = true,
                                Name = "ux_notifications_unread_user_key",
                                PartialFilterExpression = Builders<NotificationDocument>.Filter.Eq(item => item.Unread, true),
                            }),
                        new CreateIndexModel<NotificationDocument>(
                            keys.Ascending(item => item.UserId).Descending(item => item.UpdatedAt),
                            new CreateIndexOptions { Name = "ix_notifications_user_updated" }),
                        new CreateIndexModel<NotificationDocument>(
                            keys.Ascending(item => item.UserId).Ascending(item => item.Unread).Ascending(item => item.Category),
                            new CreateIndexOptions { Name = "ix_notifications_user_unread_category" }),
                        new CreateIndexModel<NotificationDocument>(
                            keys.Ascending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ttl_notifications_created", ExpireAfter = Retention }),
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

    private static Notification Map(NotificationDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            UserId = document.UserId,
            Type = document.Type,
            Category = document.Category,
            Title = document.Title,
            Body = document.Body,
            TargetKind = document.TargetKind,
            TargetId = document.TargetId,
            DedupeKey = document.DedupeKey,
            Count = document.Count,
            ReadAt = document.ReadAt is { } read ? MongoDates.FromUtc(read) : null,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableNotificationRepository : INotificationRepository
{
    public Task UpsertAsync(Notification notification, bool group, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Notification> Items, long TotalItems)> ListAsync(
        string userId,
        bool? unread,
        string? category,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<NotificationCounts> CountUnreadAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> MarkReadAsync(string userId, string id, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task MarkAllReadAsync(string userId, string? category, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task MarkReadByKeyAsync(string userId, string dedupeKey, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
