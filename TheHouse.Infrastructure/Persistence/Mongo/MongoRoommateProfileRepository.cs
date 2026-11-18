using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-22: documento de `roommate_profiles` (uno por usuario).
public sealed class RoommateProfileDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("universityId")]
    public string UniversityId { get; set; } = string.Empty;

    [BsonElement("visible")]
    public bool Visible { get; set; }

    [BsonElement("budgetMin")]
    public int BudgetMin { get; set; }

    [BsonElement("budgetMax")]
    public int BudgetMax { get; set; }

    [BsonElement("zone")]
    public string Zone { get; set; } = string.Empty;

    [BsonElement("moveIn")]
    public string MoveIn { get; set; } = string.Empty;

    [BsonElement("durationMonths")]
    public int DurationMonths { get; set; }

    [BsonElement("hasPlace")]
    public bool HasPlace { get; set; }

    [BsonElement("schedule")]
    public string Schedule { get; set; } = string.Empty;

    [BsonElement("cleaning")]
    public string Cleaning { get; set; } = string.Empty;

    [BsonElement("noise")]
    public string Noise { get; set; } = string.Empty;

    [BsonElement("smoking")]
    public string Smoking { get; set; } = string.Empty;

    [BsonElement("pets")]
    public string Pets { get; set; } = string.Empty;

    [BsonElement("social")]
    public string Social { get; set; } = string.Empty;

    [BsonElement("about")]
    [BsonIgnoreIfNull]
    public string? About { get; set; }

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class MongoRoommateProfileRepository(IMongoDatabase database) : IRoommateProfileRepository, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<RoommateProfileDocument> Collection => database.GetCollection<RoommateProfileDocument>("roommate_profiles");

    public Task<(RoommateProfile Profile, bool Created)> UpsertAsync(RoommateProfile profile, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var result = await Collection.ReplaceOneAsync(
                item => item.UserId == profile.UserId,
                ToDocument(profile),
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
            return (profile, result.UpsertedId is not null);
        });

    public Task<RoommateProfile?> GetByUserAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var document = await Collection.Find(item => item.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<RoommateProfile?> GetAsync(string id, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                return null;
            }

            var document = await Collection.Find(item => item.Id == objectId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task DeleteByUserAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteOneAsync(item => item.UserId == userId, cancellationToken));

    public Task<IReadOnlyList<RoommateProfile>> ListListedAsync(string universityId, string excludeUserId, DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var current = MongoDates.ToUtc(now);
            var documents = await Collection
                .Find(item => item.UniversityId == universityId && item.Visible && item.ExpiresAt > current && item.UserId != excludeUserId)
                .SortByDescending(item => item.UpdatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
            return (IReadOnlyList<RoommateProfile>)documents.Select(Map).ToArray();
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
                var keys = Builders<RoommateProfileDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<RoommateProfileDocument>(keys.Ascending(item => item.UserId), new CreateIndexOptions { Unique = true, Name = "ux_roommate_profiles_user" }),
                        new CreateIndexModel<RoommateProfileDocument>(
                            keys.Ascending(item => item.UniversityId).Ascending(item => item.Visible).Ascending(item => item.ExpiresAt),
                            new CreateIndexOptions { Name = "ix_roommate_profiles_university_visible_expires" }),
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

    private static RoommateProfileDocument ToDocument(RoommateProfile profile) =>
        new()
        {
            Id = ObjectId.Parse(profile.Id),
            UserId = profile.UserId,
            UniversityId = profile.UniversityId,
            Visible = profile.Visible,
            BudgetMin = profile.BudgetMin,
            BudgetMax = profile.BudgetMax,
            Zone = profile.Zone,
            MoveIn = profile.MoveIn,
            DurationMonths = profile.DurationMonths,
            HasPlace = profile.HasPlace,
            Schedule = profile.Habits.Schedule,
            Cleaning = profile.Habits.Cleaning,
            Noise = profile.Habits.Noise,
            Smoking = profile.Habits.Smoking,
            Pets = profile.Habits.Pets,
            Social = profile.Habits.Social,
            About = profile.About,
            ExpiresAt = MongoDates.ToUtc(profile.ExpiresAt),
            CreatedAt = MongoDates.ToUtc(profile.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(profile.UpdatedAt),
        };

    private static RoommateProfile Map(RoommateProfileDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            UserId = document.UserId,
            UniversityId = document.UniversityId,
            Visible = document.Visible,
            BudgetMin = document.BudgetMin,
            BudgetMax = document.BudgetMax,
            Zone = document.Zone,
            MoveIn = document.MoveIn,
            DurationMonths = document.DurationMonths,
            HasPlace = document.HasPlace,
            Habits = new RoommateHabits(document.Schedule, document.Cleaning, document.Noise, document.Smoking, document.Pets, document.Social),
            About = document.About,
            ExpiresAt = MongoDates.FromUtc(document.ExpiresAt),
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailableRoommateProfileRepository : IRoommateProfileRepository
{
    public Task<(RoommateProfile Profile, bool Created)> UpsertAsync(RoommateProfile profile, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<RoommateProfile?> GetByUserAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<RoommateProfile?> GetAsync(string id, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteByUserAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<RoommateProfile>> ListListedAsync(string universityId, string excludeUserId, DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();
}
