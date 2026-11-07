using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-15: `favorites` con índice único {userId, propertyId} (idempotencia
// ante doble toque) e índice {userId, createdAt desc, propertyId desc}.
public sealed class MongoFavoriteRepository(IMongoDatabase database) : IFavoriteRepository, IDisposable
{
    private readonly SemaphoreSlim indexGate = new(1, 1);

    private volatile bool indexReady;

    private IMongoCollection<FavoriteDocument> Collection =>
        database.GetCollection<FavoriteDocument>("favorites");

    public async Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await Collection.InsertOneAsync(
                new FavoriteDocument
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                    UserId = favorite.UserId,
                    PropertyId = favorite.PropertyId,
                    CreatedAt = MongoDates.ToUtc(favorite.CreatedAt),
                },
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public Task RemoveAsync(string userId, string propertyId, CancellationToken cancellationToken) =>
        Run(() => Collection.DeleteOneAsync(
            document => document.UserId == userId && document.PropertyId == propertyId,
            cancellationToken));

    public Task RemoveManyAsync(string userId, IReadOnlyCollection<string> propertyIds, CancellationToken cancellationToken) =>
        Run(() => Collection.DeleteManyAsync(
            Builders<FavoriteDocument>.Filter.And(
                Builders<FavoriteDocument>.Filter.Eq(document => document.UserId, userId),
                Builders<FavoriteDocument>.Filter.In(document => document.PropertyId, propertyIds)),
            cancellationToken));

    public Task<bool> ExistsAsync(string userId, string propertyId, CancellationToken cancellationToken) =>
        Run(async () => await Collection
            .Find(document => document.UserId == userId && document.PropertyId == propertyId)
            .AnyAsync(cancellationToken));

    public Task<long> CountAsync(string userId, CancellationToken cancellationToken) =>
        Run(() => Collection.CountDocumentsAsync(
            document => document.UserId == userId,
            cancellationToken: cancellationToken));

    public Task<(IReadOnlyList<Favorite> Items, long TotalItems)> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var filter = Builders<FavoriteDocument>.Filter.Eq(document => document.UserId, userId);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection
                .Find(filter)
                .Sort(Builders<FavoriteDocument>.Sort
                    .Descending(document => document.CreatedAt)
                    .Descending(document => document.PropertyId))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Favorite>)documents.Select(Map).ToArray(), total);
        });

    public Task<IReadOnlyList<string>> ListPropertyIdsAsync(string userId, int limit, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var documents = await Collection
                .Find(document => document.UserId == userId)
                .Sort(Builders<FavoriteDocument>.Sort
                    .Descending(document => document.CreatedAt)
                    .Descending(document => document.PropertyId))
                .Limit(limit)
                .Project(document => document.PropertyId)
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<string>)documents;
        });

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

            var keys = Builders<FavoriteDocument>.IndexKeys;
            await Collection.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<FavoriteDocument>(
                        keys.Ascending(document => document.UserId).Ascending(document => document.PropertyId),
                        new CreateIndexOptions { Unique = true, Name = "ux_favorites_user_property" }),
                    new CreateIndexModel<FavoriteDocument>(
                        keys.Ascending(document => document.UserId)
                            .Descending(document => document.CreatedAt)
                            .Descending(document => document.PropertyId),
                        new CreateIndexOptions { Name = "ix_favorites_user_created" }),
                ],
                cancellationToken);
            indexReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    private static Favorite Map(FavoriteDocument document) =>
        new()
        {
            UserId = document.UserId,
            PropertyId = document.PropertyId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
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
