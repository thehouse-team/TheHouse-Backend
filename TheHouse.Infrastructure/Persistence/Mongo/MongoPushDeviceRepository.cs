using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-20 1.2.0: documento de `push_devices` (una instalación por cuenta).
public sealed class PushDeviceDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("installationId")]
    public string InstallationId { get; set; } = string.Empty;

    [BsonElement("fcmToken")]
    public string FcmToken { get; set; } = string.Empty;

    [BsonElement("platform")]
    public string Platform { get; set; } = string.Empty;

    [BsonElement("appVersion")]
    [BsonIgnoreIfNull]
    public string? AppVersion { get; set; }

    [BsonElement("locale")]
    [BsonIgnoreIfNull]
    public string? Locale { get; set; }

    [BsonElement("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class MongoPushDeviceRepository(IMongoDatabase database) : IPushDeviceRepository, IDisposable
{
    // FCM considera inactivo un token que no se usa por mucho tiempo.
    public static readonly TimeSpan Retention = TimeSpan.FromDays(270);

    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    private IMongoCollection<PushDeviceDocument> Collection => database.GetCollection<PushDeviceDocument>("push_devices");

    public Task<PushDevice?> GetAsync(string userId, string installationId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await Collection.Find(item => item.UserId == userId && item.InstallationId == installationId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<PushDevice?> FindByInstallationAsync(string installationId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await Collection.Find(item => item.InstallationId == installationId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<IReadOnlyList<PushDevice>> ListAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var documents = await Collection.Find(item => item.UserId == userId).Limit(50).ToListAsync(cancellationToken);
            return (IReadOnlyList<PushDevice>)documents.Select(Map).ToList();
        });

    public Task SaveAsync(PushDevice device, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await EnsureIndexesAsync(cancellationToken);

            // Un token pertenece a una sola instalación de una sola cuenta.
            await Collection.DeleteManyAsync(
                item => item.FcmToken == device.FcmToken && !(item.UserId == device.UserId && item.InstallationId == device.InstallationId),
                cancellationToken);
            var update = Builders<PushDeviceDocument>.Update
                .SetOnInsert(item => item.Id, ObjectId.GenerateNewId())
                .SetOnInsert(item => item.RegisteredAt, MongoDates.ToUtc(device.RegisteredAt))
                .Set(item => item.FcmToken, device.FcmToken)
                .Set(item => item.Platform, device.Platform)
                .Set(item => item.AppVersion, device.AppVersion)
                .Set(item => item.Locale, device.Locale)
                .Set(item => item.UpdatedAt, MongoDates.ToUtc(device.UpdatedAt));
            await MongoCalls.RetryOnDuplicateAsync(() => Collection.UpdateOneAsync(
                item => item.UserId == device.UserId && item.InstallationId == device.InstallationId,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken));
        });

    public Task<bool> DeleteAsync(string userId, string installationId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
            (await Collection.DeleteOneAsync(item => item.UserId == userId && item.InstallationId == installationId, cancellationToken)).DeletedCount > 0);

    public Task DeleteByTokenAsync(string fcmToken, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteManyAsync(item => item.FcmToken == fcmToken, cancellationToken));

    public Task DeleteAllAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(() => Collection.DeleteManyAsync(item => item.UserId == userId, cancellationToken));

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
                var keys = Builders<PushDeviceDocument>.IndexKeys;
                await Collection.Indexes.CreateManyAsync(
                    [
                        new CreateIndexModel<PushDeviceDocument>(
                            keys.Ascending(item => item.UserId).Ascending(item => item.InstallationId),
                            new CreateIndexOptions { Unique = true, Name = "ux_push_devices_user_installation" }),
                        new CreateIndexModel<PushDeviceDocument>(
                            keys.Ascending(item => item.FcmToken),
                            new CreateIndexOptions { Unique = true, Name = "ux_push_devices_token" }),
                        new CreateIndexModel<PushDeviceDocument>(
                            keys.Ascending(item => item.UpdatedAt),
                            new CreateIndexOptions { Name = "ttl_push_devices_updated", ExpireAfter = Retention }),
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

    private static PushDevice Map(PushDeviceDocument document) =>
        new()
        {
            UserId = document.UserId,
            InstallationId = document.InstallationId,
            FcmToken = document.FcmToken,
            Platform = document.Platform,
            AppVersion = document.AppVersion,
            Locale = document.Locale,
            RegisteredAt = MongoDates.FromUtc(document.RegisteredAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => gate.Dispose();
}

public sealed class UnavailablePushDeviceRepository : IPushDeviceRepository
{
    public Task<PushDevice?> GetAsync(string userId, string installationId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<PushDevice?> FindByInstallationAsync(string installationId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<IReadOnlyList<PushDevice>> ListAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SaveAsync(PushDevice device, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<bool> DeleteAsync(string userId, string installationId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteByTokenAsync(string fcmToken, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task DeleteAllAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
