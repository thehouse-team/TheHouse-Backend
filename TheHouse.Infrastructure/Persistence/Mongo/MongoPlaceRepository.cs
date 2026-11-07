using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-14: `places` (colonias y localidades de OpenStreetMap).
public sealed class PlaceDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("nameNormalized")]
    public string NameNormalized { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("location")]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;

    [BsonElement("osmNodeId")]
    public long OsmNodeId { get; set; }
}

public sealed class MongoPlaceRepository(IMongoDatabase database) : IPlaceRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<PlaceDocument> Collection => database.GetCollection<PlaceDocument>("places");

    public Task<IReadOnlyList<Place>> SearchAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);

            // El texto del usuario se escapa: nunca se interpreta como patrón.
            var documents = await Collection
                .Find(Builders<PlaceDocument>.Filter.Regex(place => place.NameNormalized, new BsonRegularExpression(Regex.Escape(normalizedQuery))))
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<Place>)documents
                .Select(document => new Place(document.Id.ToString(), document.Name, document.Type, document.City, document.Location.Coordinates.Latitude, document.Location.Coordinates.Longitude, document.OsmNodeId))
                .ToArray();
        });

    public Task UpsertSeedAsync(IReadOnlyCollection<PlaceSeedRecord> places, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            foreach (var place in places)
            {
                await Collection.UpdateOneAsync(
                    document => document.OsmNodeId == place.OsmNodeId,
                    Builders<PlaceDocument>.Update
                        .Set(document => document.Name, place.Name)
                        .Set(document => document.NameNormalized, SearchText.Normalize(place.Name))
                        .Set(document => document.Type, place.Type)
                        .Set(document => document.City, place.City)
                        .Set(document => document.Location, GeoJson.Point(GeoJson.Geographic(place.Longitude, place.Latitude)))
                        .SetOnInsert(document => document.OsmNodeId, place.OsmNodeId),
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken);
            }
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
                var keys = Builders<PlaceDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<PlaceDocument>(keys.Ascending(place => place.OsmNodeId), new CreateIndexOptions { Unique = true, Name = "ux_places_osm" }),
                        new CreateIndexModel<PlaceDocument>(keys.Geo2DSphere(place => place.Location), new CreateIndexOptions { Name = "ix_places_location_2dsphere" }),
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

public sealed class UnavailablePlaceRepository : IPlaceRepository
{
    public Task<IReadOnlyList<Place>> SearchAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task UpsertSeedAsync(IReadOnlyCollection<PlaceSeedRecord> places, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
