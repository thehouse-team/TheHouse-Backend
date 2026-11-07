using MongoDB.Bson;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-15 1.2.0: `favorites` por cuarto con índice único {userId, roomId}.
// Al iniciar se eliminan los índices de 1.1.0 sobre `propertyId`: el único
// anterior bloquearía nuevos favoritos (todos con `propertyId` nulo).
public sealed class MongoFavoriteRepository(IMongoDatabase database) : IFavoriteRepository, IDisposable
{
    private static readonly string[] LegacyIndexes = ["ux_favorites_user_property", "ix_favorites_user_created"];

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
                    Id = ObjectId.GenerateNewId(),
                    UserId = favorite.UserId,
                    RoomId = favorite.RoomId,
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public Task RemoveAsync(string userId, string roomId, string? legacyPropertyId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteManyAsync(Match(userId, roomId, legacyPropertyId), cancellationToken));

    public Task<bool> ExistsAsync(string userId, string roomId, string? legacyPropertyId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () => await Collection.Find(Match(userId, roomId, legacyPropertyId)).AnyAsync(cancellationToken));

    public Task<long> CountAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.CountDocumentsAsync(document => document.UserId == userId, cancellationToken: cancellationToken));

    public Task<IReadOnlyList<Favorite>> ListAllAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var documents = await Collection
                .Find(document => document.UserId == userId)
                .SortByDescending(document => document.CreatedAt)
                .ThenByDescending(document => document.Id)
                .Limit(500)
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<Favorite>)documents.Select(Map).ToArray();
        });

    public Task<MigrationCounts> MigrateLegacyAsync(IReadOnlyDictionary<string, string> roomByProperty, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var legacy = await Collection
                .Find(Builders<FavoriteDocument>.Filter.And(
                    Builders<FavoriteDocument>.Filter.Exists(document => document.PropertyId),
                    Builders<FavoriteDocument>.Filter.Or(
                        Builders<FavoriteDocument>.Filter.Exists(document => document.RoomId, false),
                        Builders<FavoriteDocument>.Filter.Eq(document => document.RoomId, null))))
                .ToListAsync(cancellationToken);

            int migrated = 0, duplicates = 0, unmapped = 0;
            foreach (var document in legacy)
            {
                if (!roomByProperty.TryGetValue(document.PropertyId!, out var roomId))
                {
                    unmapped++;
                    continue;
                }

                try
                {
                    await Collection.UpdateOneAsync(
                        item => item.Id == document.Id,
                        Builders<FavoriteDocument>.Update.Set(item => item.RoomId, roomId).Unset(item => item.PropertyId),
                        cancellationToken: cancellationToken);
                    migrated++;
                }
                catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // El usuario ya tenía ese cuarto como favorito.
                    await Collection.DeleteOneAsync(item => item.Id == document.Id, cancellationToken);
                    duplicates++;
                }
            }

            return new MigrationCounts(migrated, duplicates, unmapped);
        });

    private static FilterDefinition<FavoriteDocument> Match(string userId, string roomId, string? legacyPropertyId)
    {
        var builder = Builders<FavoriteDocument>.Filter;
        var target = legacyPropertyId is null
            ? builder.Eq(document => document.RoomId, roomId)
            : builder.Or(builder.Eq(document => document.RoomId, roomId), builder.Eq(document => document.PropertyId, legacyPropertyId));
        return builder.And(builder.Eq(document => document.UserId, userId), target);
    }

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

            var existing = await (await Collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
            foreach (var name in LegacyIndexes.Where(name => existing.Any(index => index["name"].AsString == name)))
            {
                await Collection.Indexes.DropOneAsync(name, cancellationToken);
            }

            var keys = Builders<FavoriteDocument>.IndexKeys;
            await Collection.Indexes.CreateManyAsync(
                [
                    // Parcial: los favoritos de 1.1.0 sin `roomId` no chocan entre sí.
                    new CreateIndexModel<FavoriteDocument>(
                        keys.Ascending(document => document.UserId).Ascending(document => document.RoomId),
                        new CreateIndexOptions<FavoriteDocument>
                        {
                            Unique = true,
                            Name = "ux_favorites_user_room",
                            PartialFilterExpression = Builders<FavoriteDocument>.Filter.Type(document => document.RoomId, BsonType.String),
                        }),
                    new CreateIndexModel<FavoriteDocument>(
                        keys.Ascending(document => document.UserId).Descending(document => document.CreatedAt),
                        new CreateIndexOptions { Name = "ix_favorites_user_created_v2" }),
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
            RoomId = document.RoomId ?? string.Empty,
            LegacyPropertyId = document.PropertyId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
        };

    public void Dispose() => indexGate.Dispose();
}
