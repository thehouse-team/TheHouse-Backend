using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-19: documento de `reviews`. Los promedios se calculan al leer con una
// agregación sobre las publicadas.
public sealed class ReviewDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("buildingId")]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("ownerId")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("authorId")]
    public string AuthorId { get; set; } = string.Empty;

    [BsonElement("basis")]
    public string Basis { get; set; } = string.Empty;

    [BsonElement("communication")]
    public int Communication { get; set; }

    [BsonElement("attention")]
    public int Attention { get; set; }

    [BsonElement("compliance")]
    public int Compliance { get; set; }

    [BsonElement("respect")]
    public int Respect { get; set; }

    [BsonElement("cleanliness")]
    public int Cleanliness { get; set; }

    [BsonElement("location")]
    public int Location { get; set; }

    [BsonElement("value")]
    public int Value { get; set; }

    [BsonElement("condition")]
    public int Condition { get; set; }

    [BsonElement("services")]
    public int Services { get; set; }

    [BsonElement("landlordRating")]
    public double LandlordRating { get; set; }

    [BsonElement("propertyRating")]
    public double PropertyRating { get; set; }

    [BsonElement("comment")]
    [BsonIgnoreIfNull]
    public string? Comment { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "published";

    [BsonElement("edited")]
    public bool Edited { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("hiddenAt")]
    [BsonIgnoreIfNull]
    public DateTime? HiddenAt { get; set; }
}

public sealed class MongoReviewRepository(IMongoDatabase database) : IReviewRepository, IDisposable
{
    private const string Published = "published";

    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<ReviewDocument> Collection => database.GetCollection<ReviewDocument>("reviews");

    public async Task<Review?> CreateAsync(Review review, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = ToDocument(review);
            await Collection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return Map(document);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return null;
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

    public Task<Review?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<Review?> FindAsync(string authorId, string roomId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = await Collection.Find(item => item.AuthorId == authorId && item.RoomId == roomId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<bool> ReplaceIfUnchangedAsync(Review review, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var objectId = ObjectId.Parse(review.Id);
            var expected = MongoDates.ToUtc(expectedUpdatedAt);
            var result = await Collection.ReplaceOneAsync(item => item.Id == objectId && item.UpdatedAt == expected, ToDocument(review), cancellationToken: cancellationToken);
            return result.ModifiedCount == 1;
        });

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListPublishedByRoomAsync(string roomId, int page, int pageSize, CancellationToken cancellationToken) =>
        ListAsync(Builders<ReviewDocument>.Filter.Where(item => item.RoomId == roomId && item.Status == Published), page, pageSize, cancellationToken);

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListByAuthorAsync(string authorId, int page, int pageSize, CancellationToken cancellationToken) =>
        ListAsync(Builders<ReviewDocument>.Filter.Where(item => item.AuthorId == authorId), page, pageSize, cancellationToken);

    public Task<ReviewAverages> AveragesByRoomAsync(string roomId, CancellationToken cancellationToken) =>
        AveragesAsync(Builders<ReviewDocument>.Filter.Where(item => item.RoomId == roomId && item.Status == Published), cancellationToken);

    public Task<ReviewAverages> AveragesByOwnerAsync(string ownerId, CancellationToken cancellationToken) =>
        AveragesAsync(Builders<ReviewDocument>.Filter.Where(item => item.OwnerId == ownerId && item.Status == Published), cancellationToken);

    private Task<(IReadOnlyList<Review> Items, long TotalItems)> ListAsync(
        FilterDefinition<ReviewDocument> filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var total = await Collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var documents = await Collection.Find(filter)
                .SortByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToListAsync(cancellationToken);
            return ((IReadOnlyList<Review>)documents.Select(Map).ToArray(), total);
        });

    private Task<ReviewAverages> AveragesAsync(FilterDefinition<ReviewDocument> filter, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var result = await Collection.Aggregate()
                .Match(filter)
                .Group(item => 1, grouping => new
                {
                    Count = grouping.Count(),
                    Communication = grouping.Average(item => item.Communication),
                    Attention = grouping.Average(item => item.Attention),
                    Compliance = grouping.Average(item => item.Compliance),
                    Respect = grouping.Average(item => item.Respect),
                    Cleanliness = grouping.Average(item => item.Cleanliness),
                    Location = grouping.Average(item => item.Location),
                    Value = grouping.Average(item => item.Value),
                    Condition = grouping.Average(item => item.Condition),
                    Services = grouping.Average(item => item.Services),
                })
                .FirstOrDefaultAsync(cancellationToken);
            return result is null
                ? new ReviewAverages(0, null, null, null, null, null, null, null, null, null)
                : new ReviewAverages(
                    result.Count,
                    result.Communication,
                    result.Attention,
                    result.Compliance,
                    result.Respect,
                    result.Cleanliness,
                    result.Location,
                    result.Value,
                    result.Condition,
                    result.Services);
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
                var keys = Builders<ReviewDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<ReviewDocument>(
                            keys.Ascending(item => item.AuthorId).Ascending(item => item.RoomId),
                            new CreateIndexOptions { Unique = true, Name = "ux_reviews_author_room" }),
                        new CreateIndexModel<ReviewDocument>(
                            keys.Ascending(item => item.RoomId).Ascending(item => item.Status).Descending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ix_reviews_room_status_created" }),
                        new CreateIndexModel<ReviewDocument>(
                            keys.Ascending(item => item.OwnerId).Ascending(item => item.Status),
                            new CreateIndexOptions { Name = "ix_reviews_owner_status" }),
                        new CreateIndexModel<ReviewDocument>(
                            keys.Ascending(item => item.AuthorId).Descending(item => item.CreatedAt),
                            new CreateIndexOptions { Name = "ix_reviews_author_created" }),
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

    private static ReviewDocument ToDocument(Review review) =>
        new()
        {
            Id = ObjectId.Parse(review.Id),
            RoomId = review.RoomId,
            BuildingId = review.BuildingId,
            OwnerId = review.OwnerId,
            AuthorId = review.AuthorId,
            Basis = review.Basis,
            Communication = review.Landlord.Communication,
            Attention = review.Landlord.Attention,
            Compliance = review.Landlord.Compliance,
            Respect = review.Landlord.Respect,
            Cleanliness = review.Property.Cleanliness,
            Location = review.Property.Location,
            Value = review.Property.Value,
            Condition = review.Property.Condition,
            Services = review.Property.Services,
            LandlordRating = review.Landlord.Average,
            PropertyRating = review.Property.Average,
            Comment = review.Comment,
            Status = review.Status.ToString().ToLowerInvariant(),
            Edited = review.Edited,
            CreatedAt = MongoDates.ToUtc(review.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(review.UpdatedAt),
            HiddenAt = review.HiddenAt is { } hidden ? MongoDates.ToUtc(hidden) : null,
        };

    private static Review Map(ReviewDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            RoomId = document.RoomId,
            BuildingId = document.BuildingId,
            OwnerId = document.OwnerId,
            AuthorId = document.AuthorId,
            Basis = document.Basis,
            Landlord = new LandlordScores(document.Communication, document.Attention, document.Compliance, document.Respect),
            Property = new PropertyScores(document.Cleanliness, document.Location, document.Value, document.Condition, document.Services),
            Comment = document.Comment,
            Status = Enum.Parse<ReviewStatus>(document.Status, ignoreCase: true),
            Edited = document.Edited,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
            HiddenAt = document.HiddenAt is { } hidden ? MongoDates.FromUtc(hidden) : null,
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableReviewRepository : IReviewRepository
{
    public Task<Review?> CreateAsync(Review review, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Review?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<Review?> FindAsync(string authorId, string roomId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> ReplaceIfUnchangedAsync(Review review, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListPublishedByRoomAsync(string roomId, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<(IReadOnlyList<Review> Items, long TotalItems)> ListByAuthorAsync(string authorId, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task<ReviewAverages> AveragesByRoomAsync(string roomId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<ReviewAverages> AveragesByOwnerAsync(string ownerId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
