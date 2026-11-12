using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-17: `visits` (índices {userId, scheduledAt, _id} y {userId, status,
// scheduledAt}) y `visit_idempotency` (único {userId, key}, TTL de 24 h).
public sealed class MongoVisitRepository(IMongoDatabase database) : IVisitRepository, IDisposable
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim indexGate = new(1, 1);

    private volatile bool indexReady;

    private IMongoCollection<VisitDocument> Visits =>
        database.GetCollection<VisitDocument>("visits");

    private IMongoCollection<VisitIdempotencyDocument> Keys =>
        database.GetCollection<VisitIdempotencyDocument>("visit_idempotency");

    public Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = ToDocument(visit, MongoDB.Bson.ObjectId.GenerateNewId());
            await Visits.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document);
        });

    public Task<Visit?> GetAsync(string id, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Visits.Find(visit => visit.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task UpdateAsync(Visit visit, CancellationToken cancellationToken) =>
        Run(() => Visits.ReplaceOneAsync(
            document => document.Id == MongoDB.Bson.ObjectId.Parse(visit.Id),
            ToDocument(visit, MongoDB.Bson.ObjectId.Parse(visit.Id)),
            cancellationToken: cancellationToken));

    public Task<IReadOnlyList<Visit>> ListActiveAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            string[] active = [VisitStatus.Requested.ToString(), VisitStatus.Confirmed.ToString()];
            var utcNow = MongoDates.ToUtc(now);
            var builder = Builders<VisitDocument>.Filter;
            var documents = await Visits
                .Find(builder.Eq(visit => visit.UserId, userId) & builder.In(visit => visit.Status, active) & builder.Gt(visit => visit.ScheduledAt, utcNow))
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<Visit>)documents.Select(Map).ToArray();
        });

    public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListAsync(
        string userId,
        VisitStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var filter = Builders<VisitDocument>.Filter.Eq(visit => visit.UserId, userId);
            if (status is { } value)
            {
                filter &= Builders<VisitDocument>.Filter.Eq(visit => visit.Status, value.ToString());
            }

            var total = await Visits.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Visits
                .Find(filter)
                .Sort(Builders<VisitDocument>.Sort.Ascending(visit => visit.ScheduledAt).Ascending(visit => visit.Id))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Visit>)documents.Select(Map).ToArray(), total);
        });

    public Task<bool> ReplaceIfUnchangedAsync(Visit visit, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var id = MongoDB.Bson.ObjectId.Parse(visit.Id);
            var expected = MongoDates.ToUtc(expectedUpdatedAt);
            var result = await Visits.ReplaceOneAsync(
                document => document.Id == id && document.UpdatedAt == expected,
                ToDocument(visit, id),
                cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        });

    public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListForRoomsAsync(
        IReadOnlyCollection<string> roomIds,
        DateTimeOffset from,
        DateTimeOffset to,
        VisitStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var builder = Builders<VisitDocument>.Filter;
            var filter = builder.In(visit => visit.RoomId, roomIds) &
                         builder.Gte(visit => visit.ScheduledAt, MongoDates.ToUtc(from)) &
                         builder.Lte(visit => visit.ScheduledAt, MongoDates.ToUtc(to));
            if (status is { } value)
            {
                filter &= builder.Eq(visit => visit.Status, value.ToString());
            }

            var total = await Visits.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Visits
                .Find(filter)
                .Sort(Builders<VisitDocument>.Sort.Ascending(visit => visit.ScheduledAt).Ascending(visit => visit.Id))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Visit>)documents.Select(Map).ToArray(), total);
        });

    public Task<VisitIdempotencyRecord?> FindIdempotencyAsync(string userId, string key, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var document = await Keys.Find(record => record.UserId == userId && record.Key == key).FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : new VisitIdempotencyRecord(document.UserId, document.Key, document.RequestHash, document.VisitId, MongoDates.FromUtc(document.CreatedAt));
        });

    public Task<MigrationCounts> MigrateLegacyAsync(IReadOnlyDictionary<string, string> roomByProperty, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var legacy = await Visits
                .Find(Builders<VisitDocument>.Filter.And(
                    Builders<VisitDocument>.Filter.Exists(visit => visit.PropertyId),
                    Builders<VisitDocument>.Filter.Or(
                        Builders<VisitDocument>.Filter.Exists(visit => visit.RoomId, false),
                        Builders<VisitDocument>.Filter.Eq(visit => visit.RoomId, null))))
                .ToListAsync(cancellationToken);

            int migrated = 0, unmapped = 0;
            foreach (var document in legacy)
            {
                if (!roomByProperty.TryGetValue(document.PropertyId!, out var roomId))
                {
                    unmapped++;
                    continue;
                }

                await Visits.UpdateOneAsync(
                    visit => visit.Id == document.Id,
                    Builders<VisitDocument>.Update.Set(visit => visit.RoomId, roomId).Unset(visit => visit.PropertyId),
                    cancellationToken: cancellationToken);
                migrated++;
            }

            return new MigrationCounts(migrated, 0, unmapped);
        });

    public Task SaveIdempotencyAsync(VisitIdempotencyRecord record, CancellationToken cancellationToken) =>
        Run(() => Keys.InsertOneAsync(
            new VisitIdempotencyDocument
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                UserId = record.UserId,
                Key = record.Key,
                RequestHash = record.RequestHash,
                VisitId = record.VisitId,
                CreatedAt = MongoDates.ToUtc(record.CreatedAt),
            },
            cancellationToken: cancellationToken));

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (indexReady)
        {
            return;
        }

        await indexGate.WaitAsync(cancellationToken);
        try
        {
            if (indexReady)
            {
                return;
            }

            var visitKeys = Builders<VisitDocument>.IndexKeys;
            await Visits.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<VisitDocument>(
                        visitKeys.Ascending(visit => visit.UserId).Ascending(visit => visit.ScheduledAt).Ascending(visit => visit.Id),
                        new CreateIndexOptions { Name = "ix_visits_user_scheduled" }),
                    new CreateIndexModel<VisitDocument>(
                        visitKeys.Ascending(visit => visit.UserId).Ascending(visit => visit.Status).Ascending(visit => visit.ScheduledAt),
                        new CreateIndexOptions { Name = "ix_visits_user_status" }),
                    new CreateIndexModel<VisitDocument>(
                        visitKeys.Ascending(visit => visit.RoomId).Ascending(visit => visit.ScheduledAt),
                        new CreateIndexOptions { Name = "ix_visits_room_scheduled" }),
                ],
                cancellationToken);

            var keyKeys = Builders<VisitIdempotencyDocument>.IndexKeys;
            await Keys.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<VisitIdempotencyDocument>(
                        keyKeys.Ascending(record => record.UserId).Ascending(record => record.Key),
                        new CreateIndexOptions { Unique = true, Name = "ux_visit_idempotency_user_key" }),
                    new CreateIndexModel<VisitIdempotencyDocument>(
                        keyKeys.Ascending(record => record.CreatedAt),
                        new CreateIndexOptions { Name = "ttl_visit_idempotency", ExpireAfter = IdempotencyTtl }),
                ],
                cancellationToken);

            indexReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    private static VisitDocument ToDocument(Visit visit, MongoDB.Bson.ObjectId id) =>
        new()
        {
            Id = id,
            UserId = visit.UserId,
            RoomId = visit.RoomId.Length > 0 ? visit.RoomId : null,
            PropertyId = visit.LegacyPropertyId,
            ScheduledAt = MongoDates.ToUtc(visit.ScheduledAt),
            Status = visit.Status.ToString(),
            Message = visit.Message,
            CancelReason = visit.CancelReason,
            PreviousScheduledAt = visit.PreviousScheduledAt is { } previous ? MongoDates.ToUtc(previous) : null,
            CancelNote = visit.CancelNote,
            CancelledBy = visit.CancelledBy,
            RescheduleNote = visit.RescheduleNote,
            ConfirmedAt = visit.ConfirmedAt is { } confirmed ? MongoDates.ToUtc(confirmed) : null,
            CreatedAt = MongoDates.ToUtc(visit.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(visit.UpdatedAt),
            CancelledAt = visit.CancelledAt is { } cancelled ? MongoDates.ToUtc(cancelled) : null,
            History = visit.History
                .Select(entry => new VisitHistoryDocument
                {
                    Status = entry.Status.ToString(),
                    At = MongoDates.ToUtc(entry.At),
                    Actor = entry.Actor,
                })
                .ToList(),
        };

    private static Visit Map(VisitDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            UserId = document.UserId,
            RoomId = document.RoomId ?? string.Empty,
            LegacyPropertyId = document.PropertyId,
            ScheduledAt = MongoDates.FromUtc(document.ScheduledAt),
            Status = Enum.Parse<VisitStatus>(document.Status),
            Message = document.Message,
            CancelReason = document.CancelReason,
            PreviousScheduledAt = document.PreviousScheduledAt is { } previous ? MongoDates.FromUtc(previous) : null,
            CancelNote = document.CancelNote,
            CancelledBy = document.CancelledBy,
            RescheduleNote = document.RescheduleNote,
            ConfirmedAt = document.ConfirmedAt is { } confirmed ? MongoDates.FromUtc(confirmed) : null,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
            CancelledAt = document.CancelledAt is { } cancelled ? MongoDates.FromUtc(cancelled) : null,
            History = document.History
                .Select(entry => new VisitHistoryEntry(Enum.Parse<VisitStatus>(entry.Status), MongoDates.FromUtc(entry.At), entry.Actor))
                .ToArray(),
        };

    private static async Task Run(Func<Task> operation)
    {
        try
        {
            await operation();
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

    private static async Task<T> Run<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
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

    public void Dispose() => indexGate.Dispose();
}
