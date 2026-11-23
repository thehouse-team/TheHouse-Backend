using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-23 1.2.0: documentos del módulo SmartMatch.
public sealed class SmartMatchFeedbackDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("useful")]
    public bool Useful { get; set; }

    [BsonElement("modelVersion")]
    public string ModelVersion { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class SmartMatchDismissalDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// Un documento por día UTC y versión del modelo; sin ids de usuario ni de cuarto.
public sealed class SmartMatchDailyStatsDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("day")]
    public string Day { get; set; } = string.Empty;

    [BsonElement("modelVersion")]
    public string ModelVersion { get; set; } = string.Empty;

    [BsonElement("scoreRequests")]
    public long ScoreRequests { get; set; }

    [BsonElement("recommendationRequests")]
    public long RecommendationRequests { get; set; }

    [BsonElement("guestRequests")]
    public long GuestRequests { get; set; }

    [BsonElement("roomsScored")]
    public long RoomsScored { get; set; }

    [BsonElement("eligibleRooms")]
    public long EligibleRooms { get; set; }

    [BsonElement("scoreSum")]
    public long ScoreSum { get; set; }

    [BsonElement("bucket0To39")]
    public long Bucket0To39 { get; set; }

    [BsonElement("bucket40To59")]
    public long Bucket40To59 { get; set; }

    [BsonElement("bucket60To79")]
    public long Bucket60To79 { get; set; }

    [BsonElement("bucket80To100")]
    public long Bucket80To100 { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoSmartMatchRepository(IMongoDatabase database) : ISmartMatchRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<SmartMatchFeedbackDocument> Feedback => database.GetCollection<SmartMatchFeedbackDocument>("smart_match_feedback");

    private IMongoCollection<SmartMatchDismissalDocument> Dismissals => database.GetCollection<SmartMatchDismissalDocument>("smart_match_dismissals");

    private IMongoCollection<SmartMatchDailyStatsDocument> Stats => database.GetCollection<SmartMatchDailyStatsDocument>("smart_match_daily_stats");

    public Task SaveFeedbackAsync(string userId, string roomId, bool useful, string modelVersion, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var at = MongoDates.ToUtc(now);
            await Feedback.UpdateOneAsync(
                document => document.UserId == userId && document.RoomId == roomId,
                Builders<SmartMatchFeedbackDocument>.Update
                    .Set(document => document.Useful, useful)
                    .Set(document => document.ModelVersion, modelVersion)
                    .Set(document => document.UpdatedAt, at)
                    .SetOnInsert(document => document.CreatedAt, at),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        });

    public Task AddDismissalAsync(string userId, string roomId, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            await Dismissals.UpdateOneAsync(
                document => document.UserId == userId && document.RoomId == roomId,
                Builders<SmartMatchDismissalDocument>.Update.Set(document => document.CreatedAt, MongoDates.ToUtc(now)),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        });

    public Task RemoveDismissalAsync(string userId, string roomId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            await Dismissals.DeleteOneAsync(document => document.UserId == userId && document.RoomId == roomId, cancellationToken);
        });

    public Task<IReadOnlySet<string>> ListDismissedRoomIdsAsync(string userId, DateTimeOffset since, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var limit = MongoDates.ToUtc(since);
            var ids = await Dismissals
                .Find(document => document.UserId == userId && document.CreatedAt > limit)
                .Project(document => document.RoomId)
                .ToListAsync(cancellationToken);
            return (IReadOnlySet<string>)ids.ToHashSet(StringComparer.Ordinal);
        });

    public Task<long> CountActiveDismissalsAsync(DateTimeOffset since, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var limit = MongoDates.ToUtc(since);
            return await Dismissals.CountDocumentsAsync(document => document.CreatedAt > limit, cancellationToken: cancellationToken);
        });

    public Task<IReadOnlyList<SmartMatchFeedbackCount>> CountFeedbackAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var (start, end) = (MongoDates.ToUtc(from), MongoDates.ToUtc(to));
            var groups = await Feedback
                .Aggregate()
                .Match(document => document.UpdatedAt >= start && document.UpdatedAt < end)
                .Group(document => new { document.ModelVersion, document.Useful }, group => new { group.Key.ModelVersion, group.Key.Useful, Count = group.LongCount() })
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<SmartMatchFeedbackCount>)groups.Select(item => new SmartMatchFeedbackCount(item.ModelVersion, item.Useful, item.Count)).ToArray();
        });

    public Task IncrementStatsAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var id = delta.Day + "|" + delta.ModelVersion;
            await Stats.UpdateOneAsync(
                document => document.Id == id,
                Builders<SmartMatchDailyStatsDocument>.Update
                    .Inc(document => document.ScoreRequests, delta.ScoreRequests)
                    .Inc(document => document.RecommendationRequests, delta.RecommendationRequests)
                    .Inc(document => document.GuestRequests, delta.GuestRequests)
                    .Inc(document => document.RoomsScored, delta.RoomsScored)
                    .Inc(document => document.EligibleRooms, delta.EligibleRooms)
                    .Inc(document => document.ScoreSum, delta.ScoreSum)
                    .Inc(document => document.Bucket0To39, delta.Bucket0To39)
                    .Inc(document => document.Bucket40To59, delta.Bucket40To59)
                    .Inc(document => document.Bucket60To79, delta.Bucket60To79)
                    .Inc(document => document.Bucket80To100, delta.Bucket80To100)
                    .SetOnInsert(document => document.Day, delta.Day)
                    .SetOnInsert(document => document.ModelVersion, delta.ModelVersion)
                    .SetOnInsert(document => document.CreatedAt, DateTime.UtcNow),
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        });

    public Task<IReadOnlyList<SmartMatchDailyStats>> ListStatsAsync(string fromDay, string toDay, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var filter = Builders<SmartMatchDailyStatsDocument>.Filter;
            var documents = await Stats.Find(filter.Gte(document => document.Day, fromDay) & filter.Lte(document => document.Day, toDay)).ToListAsync(cancellationToken);
            return (IReadOnlyList<SmartMatchDailyStats>)documents.Select(document => new SmartMatchDailyStats(
                document.Day,
                document.ModelVersion,
                document.ScoreRequests,
                document.RecommendationRequests,
                document.GuestRequests,
                document.RoomsScored,
                document.EligibleRooms,
                document.ScoreSum,
                document.Bucket0To39,
                document.Bucket40To59,
                document.Bucket60To79,
                document.Bucket80To100)).ToArray();
        });

    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            await Feedback.DeleteManyAsync(document => document.UserId == userId, cancellationToken);
            await Dismissals.DeleteManyAsync(document => document.UserId == userId, cancellationToken);
        });

    public void Dispose() => gate.Dispose();

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
                var feedback = Builders<SmartMatchFeedbackDocument>.IndexKeys;
                await Feedback.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<SmartMatchFeedbackDocument>(
                            feedback.Ascending(document => document.UserId).Ascending(document => document.RoomId),
                            new CreateIndexOptions { Unique = true, Name = "ux_smart_match_feedback_user_room" }),
                        new CreateIndexModel<SmartMatchFeedbackDocument>(
                            feedback.Ascending(document => document.UpdatedAt),
                            new CreateIndexOptions { Name = "ix_smart_match_feedback_updated" }),
                    ],
                    cancellationToken);

                var dismissals = Builders<SmartMatchDismissalDocument>.IndexKeys;
                await Dismissals.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<SmartMatchDismissalDocument>(
                            dismissals.Ascending(document => document.UserId).Ascending(document => document.RoomId),
                            new CreateIndexOptions { Unique = true, Name = "ux_smart_match_dismissals_user_room" }),
                        new CreateIndexModel<SmartMatchDismissalDocument>(
                            dismissals.Ascending(document => document.CreatedAt),
                            new CreateIndexOptions { Name = "ttl_smart_match_dismissals_created", ExpireAfter = SmartMatchRules.DismissalDuration }),
                    ],
                    cancellationToken);

                var stats = Builders<SmartMatchDailyStatsDocument>.IndexKeys;
                await Stats.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<SmartMatchDailyStatsDocument>(
                            stats.Ascending(document => document.Day),
                            new CreateIndexOptions { Name = "ix_smart_match_daily_stats_day" }),
                        new CreateIndexModel<SmartMatchDailyStatsDocument>(
                            stats.Ascending(document => document.CreatedAt),
                            new CreateIndexOptions { Name = "ttl_smart_match_daily_stats_created", ExpireAfter = SmartMatchRules.StatsRetention }),
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
}

public sealed class UnavailableSmartMatchRepository : ISmartMatchRepository
{
    public Task SaveFeedbackAsync(string userId, string roomId, bool useful, string modelVersion, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task AddDismissalAsync(string userId, string roomId, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task RemoveDismissalAsync(string userId, string roomId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlySet<string>> ListDismissedRoomIdsAsync(string userId, DateTimeOffset since, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<long> CountActiveDismissalsAsync(DateTimeOffset since, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<SmartMatchFeedbackCount>> CountFeedbackAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task IncrementStatsAsync(SmartMatchDailyStats delta, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<SmartMatchDailyStats>> ListStatsAsync(string fromDay, string toDay, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
