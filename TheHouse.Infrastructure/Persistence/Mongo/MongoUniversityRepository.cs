using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class MongoUniversityRepository(
    IMongoDatabase database) :
    IUniversityRepository,
    IDisposable
{
    private const string CodeIndexName =
        "ux_universities_code";

    private const string OrderIndexName =
        "ix_universities_name_id_asc";

    private const string LocationIndexName =
        "ix_universities_location_2dsphere";

    private static readonly
        ProjectionDefinition<UniversityDocument>
        PublicProjection =
            Builders<UniversityDocument>.Projection
                .Include(university => university.Id)
                .Include(university => university.Code)
                .Include(university => university.Name)
                .Include(university => university.City)
                .Include(university => university.Country)
                .Include(university => university.CreatedAt)
                .Include(university => university.Location);

    private readonly SemaphoreSlim indexGate =
        new(1, 1);

    private volatile bool indexesReady;

    private IMongoCollection<UniversityDocument> Collection =>
        database.GetCollection<UniversityDocument>(
            "universities");

    public async Task<(
        IReadOnlyList<University> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        UniversityQueryValidation.EnsureValidPagination(
            page,
            pageSize);

        try
        {
            await EnsureIndexesAsync(
                cancellationToken);

            var totalItems =
                await Collection.CountDocumentsAsync(
                    FilterDefinition<UniversityDocument>.Empty,
                    cancellationToken:
                        cancellationToken);

            var skip =
                ((long)page - 1L) * pageSize;

            if (skip > int.MaxValue)
            {
                return (
                    Array.Empty<University>(),
                    totalItems);
            }

            var documents =
                await Collection
                    .Find(
                        FilterDefinition<UniversityDocument>
                            .Empty)
                    .Sort(
                        Builders<UniversityDocument>.Sort
                            .Ascending(
                                university =>
                                    university.Name)
                            .Ascending(
                                university =>
                                    university.Id))
                    .Skip((int)skip)
                    .Limit(pageSize)
                    .Project<UniversityDocument>(
                        PublicProjection)
                    .ToListAsync(
                        cancellationToken);

            return (
                documents
                    .Select(Map)
                    .ToArray(),
                totalItems);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<University?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!UniversityQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException(
                "id",
                "id must be a 24-character hexadecimal ObjectId.");
        }

        try
        {
            await EnsureIndexesAsync(
                cancellationToken);

            var objectId =
                MongoDB.Bson.ObjectId.Parse(id);

            var document =
                await Collection
                    .Find(university =>
                        university.Id == objectId)
                    .Project<UniversityDocument>(
                        PublicProjection)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            return document is null
                ? null
                : Map(document);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<IReadOnlyList<University>> SearchByTextAsync(
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(
                cancellationToken);

            // El texto del usuario se escapa: nunca se interpreta como patrón.
            var pattern =
                new BsonRegularExpression(
                    Regex.Escape(normalizedQuery));

            var documents =
                await Collection
                    .Find(
                        Builders<UniversityDocument>.Filter.Regex(
                            university => university.SearchKey,
                            pattern))
                    .Sort(
                        Builders<UniversityDocument>.Sort
                            .Ascending(university => university.Name)
                            .Ascending(university => university.Id))
                    .Limit(limit)
                    .Project<UniversityDocument>(
                        PublicProjection)
                    .ToListAsync(
                        cancellationToken);

            return documents
                .Select(Map)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<IReadOnlyList<University>> SearchNearAsync(
        double latitude,
        double longitude,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(
                cancellationToken);

            // $nearSphere devuelve los documentos ya ordenados por distancia.
            var documents =
                await Collection
                    .Find(
                        Builders<UniversityDocument>.Filter.NearSphere(
                            university => university.Location,
                            GeoJson.Point(
                                GeoJson.Geographic(
                                    longitude,
                                    latitude))))
                    .Limit(limit)
                    .Project<UniversityDocument>(
                        PublicProjection)
                    .ToListAsync(
                        cancellationToken);

            return documents
                .Select(Map)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task UpsertSeedAsync(
        IReadOnlyCollection<University> universities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            universities);

        try
        {
            await EnsureIndexesAsync(
                cancellationToken);

            foreach (var university in universities)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var update =
                    Builders<UniversityDocument>.Update
                        .Set(
                            document => document.Code,
                            university.Code)
                        .Set(
                            document => document.Name,
                            university.Name)
                        .Set(
                            document => document.City,
                            university.City)
                        .Set(
                            document => document.Country,
                            university.Country)
                        .Set(
                            document => document.SearchKey,
                            SearchText.UniversityKey(
                                university.Code,
                                university.Name,
                                university.City))
                        .Set(
                            document => document.Location,
                            university.Latitude is { } lat &&
                            university.Longitude is { } lng
                                ? GeoJson.Point(
                                    GeoJson.Geographic(lng, lat))
                                : null)
                        .SetOnInsert(
                            document => document.CreatedAt,
                            MongoDates.ToUtc(
                                university.CreatedAt));

                await Collection.UpdateOneAsync(
                    document =>
                        document.Code ==
                        university.Code,
                    update,
                    new UpdateOptions
                    {
                        IsUpsert = true
                    },
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  MongoException or
                  TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    private async Task EnsureIndexesAsync(
        CancellationToken cancellationToken)
    {
        if (indexesReady)
        {
            return;
        }

        await indexGate.WaitAsync(
            cancellationToken);

        try
        {
            if (indexesReady)
            {
                return;
            }

            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<UniversityDocument>(
                    Builders<UniversityDocument>
                        .IndexKeys
                        .Ascending(
                            university =>
                                university.Code),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Name = CodeIndexName
                    }),
                cancellationToken:
                    cancellationToken);

            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<UniversityDocument>(
                    Builders<UniversityDocument>
                        .IndexKeys
                        .Ascending(
                            university =>
                                university.Name)
                        .Ascending(
                            university =>
                                university.Id),
                    new CreateIndexOptions
                    {
                        Name = OrderIndexName
                    }),
                cancellationToken:
                    cancellationToken);

            // NV-11: índice geoespacial para la búsqueda por cercanía.
            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<UniversityDocument>(
                    Builders<UniversityDocument>
                        .IndexKeys
                        .Geo2DSphere(
                            university =>
                                university.Location),
                    new CreateIndexOptions
                    {
                        Name = LocationIndexName
                    }),
                cancellationToken:
                    cancellationToken);

            indexesReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    private static University Map(
        UniversityDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            Code = document.Code,
            Name = document.Name,
            City = document.City,
            Country = document.Country,
            CreatedAt =
                MongoDates.FromUtc(
                    document.CreatedAt),
            Latitude =
                document.Location?.Coordinates.Latitude,
            Longitude =
                document.Location?.Coordinates.Longitude
        };

    public void Dispose()
    {
        indexGate.Dispose();
    }
}
