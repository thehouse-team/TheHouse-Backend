using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-24 1.2.1: caché `smart_match_explanations` (TTL 24 h). La llave es un
// SHA-256 de cuarto, preferencias y versiones: no guarda preferencias, ids de
// usuario ni textos del arrendador.
public sealed class SmartMatchExplanationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("pros")]
    public List<string> Pros { get; set; } = [];

    [BsonElement("cons")]
    public List<string> Cons { get; set; } = [];

    [BsonElement("generatedBy")]
    public string GeneratedBy { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public sealed class MongoSmartMatchExplanationCache(IMongoDatabase database) : ISmartMatchExplanationCache, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<SmartMatchExplanationDocument> Collection => database.GetCollection<SmartMatchExplanationDocument>("smart_match_explanations");

    public Task<CachedExplanation?> GetAsync(string key, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            var since = MongoDates.ToUtc(now - SmartMatchExplanationRules.CacheDuration);
            var document = await Collection.Find(item => item.Id == key && item.CreatedAt > since).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : new CachedExplanation(document.Summary, document.Pros, document.Cons, document.GeneratedBy);
        });

    public Task SetAsync(string key, CachedExplanation explanation, DateTimeOffset now, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);
            await Collection.ReplaceOneAsync(
                item => item.Id == key,
                new SmartMatchExplanationDocument
                {
                    Id = key,
                    Summary = explanation.Summary,
                    Pros = [.. explanation.Pros],
                    Cons = [.. explanation.Cons],
                    GeneratedBy = explanation.GeneratedBy,
                    CreatedAt = MongoDates.ToUtc(now),
                },
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        });

    public void Dispose() => gate.Dispose();

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
                await Collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<SmartMatchExplanationDocument>(
                        Builders<SmartMatchExplanationDocument>.IndexKeys.Ascending(item => item.CreatedAt),
                        new CreateIndexOptions { Name = "ttl_smart_match_explanations_created", ExpireAfter = SmartMatchExplanationRules.CacheDuration }),
                    cancellationToken: cancellationToken);
                ready = true;
            }
        }
        finally
        {
            gate.Release();
        }
    }
}

public sealed class UnavailableSmartMatchExplanationCache : ISmartMatchExplanationCache
{
    public Task<CachedExplanation?> GetAsync(string key, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SetAsync(string key, CachedExplanation explanation, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
