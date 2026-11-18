using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-25: documento de `room_views` (una vista por persona, cuarto y día).
public sealed class RoomViewDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("day")]
    public string Day { get; set; } = string.Empty;

    [BsonElement("actorHash")]
    public string ActorHash { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoRoomViewRepository(IMongoDatabase database) : IRoomViewRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<RoomViewDocument> Collection => database.GetCollection<RoomViewDocument>("room_views");

    public Task<bool> AddAsync(RoomView view, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            try
            {
                await Collection.InsertOneAsync(
                    new RoomViewDocument
                    {
                        Id = ObjectId.GenerateNewId(),
                        RoomId = view.RoomId,
                        BuildingId = view.BuildingId,
                        OwnerId = view.OwnerId,
                        Day = view.Day,
                        ActorHash = view.ActorHash,
                        CreatedAt = MongoDates.ToUtc(view.CreatedAt),
                    },
                    cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        });

    public Task<IReadOnlyList<RoomViewCount>> CountAsync(string ownerId, string fromDay, string toDay, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var filter = Builders<RoomViewDocument>.Filter;
            var groups = await Collection
                .Aggregate()
                .Match(filter.Eq(item => item.OwnerId, ownerId) & filter.Gte(item => item.Day, fromDay) & filter.Lte(item => item.Day, toDay))
                .Group(
                    item => new { item.RoomId, item.BuildingId, item.Day },
                    group => new { group.Key.RoomId, group.Key.BuildingId, group.Key.Day, Count = group.Count() })
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<RoomViewCount>)groups.Select(item => new RoomViewCount(item.RoomId, item.BuildingId, item.Day, item.Count)).ToArray();
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
                var keys = Builders<RoomViewDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<RoomViewDocument>(
                            keys.Ascending(item => item.RoomId).Ascending(item => item.BuildingId).Ascending(item => item.Day).Ascending(item => item.ActorHash),
                            new CreateIndexOptions { Unique = true, Name = "ux_room_views_room_day_actor" }),
                        new CreateIndexModel<RoomViewDocument>(
                            keys.Ascending(item => item.OwnerId).Ascending(item => item.Day),
                            new CreateIndexOptions { Name = "ix_room_views_owner_day" }),
                        new CreateIndexModel<RoomViewDocument>(
                            keys.Ascending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ttl_room_views_created", ExpireAfter = LandlordStatsRules.ViewRetention }),
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

    public Task DeleteByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteManyAsync(item => item.OwnerId == ownerId, cancellationToken));

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableRoomViewRepository : IRoomViewRepository
{
    public Task<bool> AddAsync(RoomView view, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<RoomViewCount>> CountAsync(string ownerId, string fromDay, string toDay, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task DeleteByOwnerAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
