using MongoDB.Driver;
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

    private static readonly
        ProjectionDefinition<UniversityDocument>
        PublicProjection =
            Builders<UniversityDocument>.Projection
                .Include(university => university.Id)
                .Include(university => university.Code)
                .Include(university => university.Name)
                .Include(university => university.City)
                .Include(university => university.Country)
                .Include(university => university.CreatedAt);

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
                    document.CreatedAt)
        };

    public void Dispose()
    {
        indexGate.Dispose();
    }
}
