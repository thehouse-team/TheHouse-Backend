using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-12: `landlord_profiles` con índice único {userId}.
public sealed class MongoLandlordProfileRepository(IMongoDatabase database) : ILandlordProfileRepository, IDisposable
{
    private readonly UniqueUserIndex<LandlordProfileDocument> index =
        new(database.GetCollection<LandlordProfileDocument>("landlord_profiles"), "ux_landlord_profiles_user");

    public Task<LandlordProfile?> GetAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await index.Collection.Find(profile => profile.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : new LandlordProfile
                {
                    UserId = document.UserId,
                    FullName = document.FullName,
                    Phone = document.Phone,
                    RentalMode = document.RentalMode,
                    Rfc = document.Rfc,
                    AcceptedTermsVersion = document.AcceptedTermsVersion,
                    AcceptedTermsAt = MongoDates.FromUtc(document.AcceptedTermsAt),
                    CreatedAt = MongoDates.FromUtc(document.CreatedAt),
                };
        });

    public async Task<bool> CreateAsync(LandlordProfile profile, CancellationToken cancellationToken)
    {
        try
        {
            await index.EnsureAsync(cancellationToken);
            await index.Collection.InsertOneAsync(
                new LandlordProfileDocument
                {
                    Id = ObjectId.GenerateNewId(),
                    UserId = profile.UserId,
                    FullName = profile.FullName,
                    Phone = profile.Phone,
                    RentalMode = profile.RentalMode,
                    Rfc = profile.Rfc,
                    AcceptedTermsVersion = profile.AcceptedTermsVersion,
                    AcceptedTermsAt = MongoDates.ToUtc(profile.AcceptedTermsAt),
                    CreatedAt = MongoDates.ToUtc(profile.CreatedAt),
                },
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
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

    public void Dispose() => index.Dispose();
}

// NV-12: `buildings`; reemplazos condicionados a `version`.
public sealed class MongoBuildingRepository(IMongoDatabase database) : IBuildingRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<BuildingDocument> Collection => database.GetCollection<BuildingDocument>("buildings");

    public Task<Building> CreateAsync(Building building, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = ToDocument(building, ObjectId.GenerateNewId());
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document);
        });

    public Task<Building?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(building => building.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<(IReadOnlyList<Building> Items, long TotalItems)> ListByOwnerAsync(string ownerId, int page, int pageSize, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var filter = Builders<BuildingDocument>.Filter.Eq(building => building.OwnerId, ownerId);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .Sort(Builders<BuildingDocument>.Sort.Descending(building => building.CreatedAt).Descending(building => building.Id))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Building>)documents.Select(Map).ToArray(), total);
        });

    public Task<IReadOnlyList<Building>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var documents = await Collection.Find(building => building.OwnerId == ownerId).ToListAsync(cancellationToken);
            return (IReadOnlyList<Building>)documents.Select(Map).ToArray();
        });

    public Task<long> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.CountDocumentsAsync(building => building.OwnerId == ownerId, cancellationToken: cancellationToken));

    public Task<bool> ReplaceAsync(Building building, long expectedVersion, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var id = ObjectId.Parse(building.Id);
            var result = await Collection.ReplaceOneAsync(
                document => document.Id == id && document.Version == expectedVersion,
                ToDocument(building, id),
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        });

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(id);
            var result = await Collection.DeleteOneAsync(
                document => document.Id == objectId && document.Version == expectedVersion,
                cancellationToken);
            return result.DeletedCount == 1;
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
                var keys = Builders<BuildingDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<BuildingDocument>(
                            keys.Ascending(building => building.OwnerId).Descending(building => building.CreatedAt),
                            new CreateIndexOptions { Name = "ix_buildings_owner_created" }),
                        new CreateIndexModel<BuildingDocument>(
                            keys.Geo2DSphere(building => building.Location),
                            new CreateIndexOptions { Name = "ix_buildings_location_2dsphere" }),
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

    private static BuildingDocument ToDocument(Building building, ObjectId id) =>
        new()
        {
            Id = id,
            OwnerId = building.OwnerId,
            Type = building.Type,
            Name = building.Name,
            Address = new AddressDocument
            {
                Street = building.Address.Street,
                Number = building.Address.Number,
                Neighborhood = building.Address.Neighborhood,
                City = building.Address.City,
                State = building.Address.State,
                PostalCode = building.Address.PostalCode,
            },
            Location = GeoJson.Point(GeoJson.Geographic(building.Longitude, building.Latitude)),
            NearestUniversityId = building.NearestUniversityId,
            DistanceToUniversityKm = building.DistanceToUniversityKm,
            WalkingMinutes = building.WalkingMinutes,
            AllowedTenants = building.AllowedTenants,
            Services = building.Services.ToList(),
            Rules = building.Rules,
            Status = building.Status.ToString(),
            Version = building.Version,
            CreatedAt = MongoDates.ToUtc(building.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(building.UpdatedAt),
        };

    private static Building Map(BuildingDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            OwnerId = document.OwnerId,
            Type = document.Type,
            Name = document.Name,
            Address = new BuildingAddress(document.Address.Street, document.Address.Number, document.Address.Neighborhood, document.Address.City, document.Address.State, document.Address.PostalCode),
            Latitude = document.Location.Coordinates.Latitude,
            Longitude = document.Location.Coordinates.Longitude,
            NearestUniversityId = document.NearestUniversityId,
            DistanceToUniversityKm = document.DistanceToUniversityKm,
            WalkingMinutes = document.WalkingMinutes,
            AllowedTenants = document.AllowedTenants,
            Services = document.Services.ToArray(),
            Rules = document.Rules,
            Status = Enum.Parse<BuildingStatus>(document.Status),
            Version = document.Version,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

// NV-12: `rooms` con índice único {buildingId, number}.
public sealed class MongoRoomRepository(IMongoDatabase database) : IRoomRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<RoomDocument> Collection => database.GetCollection<RoomDocument>("rooms");

    public async Task<IReadOnlyList<Room>> CreateManyAsync(IReadOnlyList<Room> rooms, CancellationToken cancellationToken)
    {
        var documents = rooms.Select(room => ToDocument(room, ObjectId.GenerateNewId())).ToList();
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await Collection.InsertManyAsync(documents, new InsertManyOptions { IsOrdered = true }, cancellationToken);
            return documents.Select(Map).ToArray();
        }
        catch (MongoBulkWriteException exception) when (exception.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey))
        {
            // Generación concurrente: se retiran los cuartos que esta misma
            // llamada alcanzó a insertar, para no dejar una generación a medias.
            var ids = documents.Select(document => document.Id).ToList();
            await Collection.DeleteManyAsync(Builders<RoomDocument>.Filter.In(document => document.Id, ids), CancellationToken.None);
            throw new ConflictException("room_number_taken");
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

    public Task<Room?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(room => room.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<(IReadOnlyList<Room> Items, long TotalItems)> ListByBuildingAsync(string buildingId, int page, int pageSize, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var filter = Builders<RoomDocument>.Filter.Eq(room => room.BuildingId, buildingId);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .Sort(Builders<RoomDocument>.Sort.Ascending(room => room.Floor).Ascending(room => room.Number))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Room>)documents.Select(Map).ToArray(), total);
        });

    public Task<IReadOnlyList<Room>> ListAllByBuildingAsync(string buildingId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var documents = await Collection.Find(room => room.BuildingId == buildingId).ToListAsync(cancellationToken);
            return (IReadOnlyList<Room>)documents.Select(Map).ToArray();
        });

    public Task<IReadOnlyList<Room>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var documents = await Collection.Find(room => room.OwnerId == ownerId).ToListAsync(cancellationToken);
            return (IReadOnlyList<Room>)documents.Select(Map).ToArray();
        });

    public async Task<bool> ReplaceAsync(Room room, long expectedVersion, CancellationToken cancellationToken)
    {
        try
        {
            var id = ObjectId.Parse(room.Id);
            var result = await Collection.ReplaceOneAsync(
                document => document.Id == id && document.Version == expectedVersion,
                ToDocument(room, id),
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new ConflictException("room_number_taken");
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

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(id);
            var result = await Collection.DeleteOneAsync(document => document.Id == objectId && document.Version == expectedVersion, cancellationToken);
            return result.DeletedCount == 1;
        });

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteManyAsync(room => room.BuildingId == buildingId, cancellationToken));

    public Task SetStatusByBuildingAsync(string buildingId, RoomStatus from, RoomStatus to, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.UpdateManyAsync(
            room => room.BuildingId == buildingId && room.Status == from.ToString(),
            Builders<RoomDocument>.Update
                .Set(room => room.Status, to.ToString())
                .Set(room => room.UpdatedAt, MongoDates.ToUtc(now))
                .Inc(room => room.Version, 1L),
            cancellationToken: cancellationToken));

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
                var keys = Builders<RoomDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<RoomDocument>(
                            keys.Ascending(room => room.BuildingId).Ascending(room => room.Number),
                            new CreateIndexOptions { Unique = true, Name = "ux_rooms_building_number" }),
                        new CreateIndexModel<RoomDocument>(
                            keys.Ascending(room => room.OwnerId).Ascending(room => room.Status),
                            new CreateIndexOptions { Name = "ix_rooms_owner_status" }),
                        new CreateIndexModel<RoomDocument>(
                            keys.Ascending(room => room.Status).Ascending(room => room.Price),
                            new CreateIndexOptions { Name = "ix_rooms_status_price" }),
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

    private static RoomDocument ToDocument(Room room, ObjectId id) =>
        new()
        {
            Id = id,
            BuildingId = room.BuildingId,
            OwnerId = room.OwnerId,
            Number = room.Number,
            Floor = room.Floor,
            Price = room.Price,
            SizeM2 = room.SizeM2,
            Type = room.Type,
            Bathroom = room.Bathroom,
            Furniture = room.Furniture.ToList(),
            AvailableFrom = room.AvailableFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            MinContract = room.MinContract,
            Status = room.Status.ToString(),
            Version = room.Version,
            CreatedAt = MongoDates.ToUtc(room.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(room.UpdatedAt),
        };

    private static Room Map(RoomDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            BuildingId = document.BuildingId,
            OwnerId = document.OwnerId,
            Number = document.Number,
            Floor = document.Floor,
            Price = document.Price,
            SizeM2 = document.SizeM2,
            Type = document.Type,
            Bathroom = document.Bathroom,
            Furniture = document.Furniture.ToArray(),
            AvailableFrom = document.AvailableFrom is null ? null : DateOnly.ParseExact(document.AvailableFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            MinContract = document.MinContract,
            Status = Enum.Parse<RoomStatus>(document.Status),
            Version = document.Version,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

// Sin MongoDB habilitado, todo el módulo falla cerrado (503).
public sealed class UnavailableLandlordProfileRepository : ILandlordProfileRepository
{
    public Task<LandlordProfile?> GetAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> CreateAsync(LandlordProfile profile, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}

public sealed class UnavailableBuildingRepository : IBuildingRepository
{
    public Task<Building> CreateAsync(Building building, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Building?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Building> Items, long TotalItems)> ListByOwnerAsync(string ownerId, int page, int pageSize, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<Building>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<long> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> ReplaceAsync(Building building, long expectedVersion, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}

public sealed class UnavailableRoomRepository : IRoomRepository
{
    public Task<IReadOnlyList<Room>> CreateManyAsync(IReadOnlyList<Room> rooms, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Room?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Room> Items, long TotalItems)> ListByBuildingAsync(string buildingId, int page, int pageSize, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<Room>> ListAllByBuildingAsync(string buildingId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<Room>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> ReplaceAsync(Room room, long expectedVersion, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SetStatusByBuildingAsync(string buildingId, RoomStatus from, RoomStatus to, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
