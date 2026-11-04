using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class MongoPropertyRepository(
    IMongoDatabase database) :
    IPropertyRepository,
    IDisposable
{
    private const string IndexName =
        "ix_properties_createdAt_id_desc";

    private static readonly
        ProjectionDefinition<PropertyDocument>
        PublicProjection =
            Builders<PropertyDocument>.Projection
                .Include(property => property.Id)
                .Include(property => property.Title)
                .Include(property => property.Description)
                .Include(property => property.Price)
                .Include(property => property.Currency)
                .Include(property => property.City)
                .Include(property => property.UniversityId)
                .Include(property => property.Bedrooms)
                .Include(property => property.Bathrooms)
                .Include(property => property.AreaM2)
                .Include(property => property.Amenities)
                .Include(property => property.CreatedAt);

    private readonly SemaphoreSlim indexGate =
        new(1, 1);

    private volatile bool indexReady;

    private IMongoCollection<PropertyDocument> Collection =>
        database.GetCollection<PropertyDocument>("properties");

    public async Task<(
        IReadOnlyList<Property> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(
            page,
            pageSize);

        try
        {
            await EnsureIndexAsync(cancellationToken);

            var totalItems =
                await Collection.CountDocumentsAsync(
                    FilterDefinition<PropertyDocument>.Empty,
                    cancellationToken:
                        cancellationToken);

            var skip =
                ((long)page - 1L) * pageSize;

            if (skip > int.MaxValue)
            {
                return (
                    Array.Empty<Property>(),
                    totalItems);
            }

            var documents =
                await Collection
                    .Find(
                        FilterDefinition<PropertyDocument>
                            .Empty)
                    .Sort(
                        Builders<PropertyDocument>.Sort
                            .Descending(
                                property =>
                                    property.CreatedAt)
                            .Descending(
                                property =>
                                    property.Id))
                    .Skip((int)skip)
                    .Limit(pageSize)
                    .Project<PropertyDocument>(
                        PublicProjection)
                    .ToListAsync(cancellationToken);

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

    public async Task<Property?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!PropertyQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException(
                "id",
                "id must be a 24-character hexadecimal ObjectId.");
        }

        try
        {
            var objectId =
                MongoDB.Bson.ObjectId.Parse(id);

            var document =
                await Collection
                    .Find(property =>
                        property.Id == objectId)
                    .Project<PropertyDocument>(
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

    private async Task EnsureIndexAsync(
        CancellationToken cancellationToken)
    {
        if (indexReady)
        {
            return;
        }

        await indexGate.WaitAsync(
            cancellationToken);

        try
        {
            if (indexReady)
            {
                return;
            }

            var keys =
                Builders<PropertyDocument>.IndexKeys
                    .Descending(property =>
                        property.CreatedAt)
                    .Descending(property =>
                        property.Id);

            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<PropertyDocument>(
                    keys,
                    new CreateIndexOptions
                    {
                        Name = IndexName
                    }),
                cancellationToken:
                    cancellationToken);

            indexReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    private static Property Map(
        PropertyDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            Title = document.Title,
            Description = document.Description,
            Price = document.Price,
            Currency = document.Currency,
            City = document.City,
            UniversityId = document.UniversityId,
            Bedrooms = document.Bedrooms,
            Bathrooms = document.Bathrooms,
            AreaM2 = document.AreaM2,
            Amenities =
                document.Amenities ??
                Array.Empty<string>(),
            CreatedAt =
                MongoDates.FromUtc(
                    document.CreatedAt)
        };

    public void Dispose()
    {
        indexGate.Dispose();
    }
}
