using MongoDB.Driver;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-10: refresh tokens en MongoDB.
//  - Índice único en `tokenHash`; índice por `sessionId`.
//  - Índice TTL en `expiresAt`: MongoDB borra los tokens vencidos.
//  - La rotación es una actualización condicional atómica
//    (FindOneAndUpdate), de modo que dos solicitudes simultáneas con el
//    mismo token producen como máximo una rotación.
public sealed class MongoRefreshTokenStore(IMongoDatabase database) : IRefreshTokenStore, IDisposable
{
    public const string CollectionName = "refresh_tokens";

    private readonly SemaphoreSlim indexGate = new(1, 1);

    private volatile bool indexReady;

    private IMongoCollection<RefreshTokenDocument> Collection =>
        database.GetCollection<RefreshTokenDocument>(CollectionName);

    public async Task CreateAsync(
        RefreshToken token,
        CancellationToken cancellationToken)
    {
        var document = new RefreshTokenDocument
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            TokenHash = token.TokenHash,
            SessionId = token.SessionId,
            UserId = token.UserId,
            CreatedAt = MongoDates.ToUtc(token.CreatedAt),
            ExpiresAt = MongoDates.ToUtc(token.ExpiresAt),
            SessionExpiresAt = MongoDates.ToUtc(token.SessionExpiresAt),
        };

        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await Collection.InsertOneAsync(
                document,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<RefreshToken?> TryRotateAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var utcNow = MongoDates.ToUtc(now);
        var filter = Builders<RefreshTokenDocument>.Filter.And(
            Builders<RefreshTokenDocument>.Filter.Eq(token => token.TokenHash, tokenHash),
            Builders<RefreshTokenDocument>.Filter.Eq(token => token.RotatedAt, null),
            Builders<RefreshTokenDocument>.Filter.Eq(token => token.RevokedAt, null),
            Builders<RefreshTokenDocument>.Filter.Gt(token => token.ExpiresAt, utcNow),
            Builders<RefreshTokenDocument>.Filter.Gt(token => token.SessionExpiresAt, utcNow));

        try
        {
            var document = await Collection.FindOneAndUpdateAsync(
                filter,
                Builders<RefreshTokenDocument>.Update.Set(token => token.RotatedAt, utcNow),
                new FindOneAndUpdateOptions<RefreshTokenDocument>
                {
                    ReturnDocument = ReturnDocument.After,
                },
                cancellationToken);

            return document is null ? null : Map(document);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<RefreshToken?> FindAsync(
        string tokenHash,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await Collection
                .Find(token => token.TokenHash == tokenHash)
                .FirstOrDefaultAsync(cancellationToken);

            return document is null ? null : Map(document);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<IReadOnlyList<string>> ListActiveSessionIdsAsync(
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var utcNow = MongoDates.ToUtc(now);
            var sessions = await Collection
                .Distinct(
                    token => token.SessionId,
                    token => token.UserId == userId && token.RevokedAt == null && token.SessionExpiresAt > utcNow,
                    cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
            return sessions;
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task RevokeSessionAsync(
        string sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            await Collection.UpdateManyAsync(
                token => token.SessionId == sessionId && token.RevokedAt == null,
                Builders<RefreshTokenDocument>.Update
                    .Set(token => token.RevokedAt, MongoDates.ToUtc(now))
                    .Set(token => token.RevokedReason, reason),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (indexReady)
        {
            return;
        }

        await indexGate.WaitAsync(cancellationToken);
        try
        {
            if (indexReady)
            {
                return;
            }

            var keys = Builders<RefreshTokenDocument>.IndexKeys;
            await Collection.Indexes.CreateManyAsync(
                [
                    new CreateIndexModel<RefreshTokenDocument>(
                        keys.Ascending(token => token.TokenHash),
                        new CreateIndexOptions { Unique = true, Name = "ux_refresh_tokens_hash" }),
                    new CreateIndexModel<RefreshTokenDocument>(
                        keys.Ascending(token => token.SessionId),
                        new CreateIndexOptions { Name = "ix_refresh_tokens_session" }),
                    new CreateIndexModel<RefreshTokenDocument>(
                        keys.Ascending(token => token.ExpiresAt),
                        new CreateIndexOptions { Name = "ttl_refresh_tokens_expires", ExpireAfter = TimeSpan.Zero }),
                ],
                cancellationToken);

            indexReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    private static RefreshToken Map(RefreshTokenDocument document) =>
        new()
        {
            TokenHash = document.TokenHash,
            SessionId = document.SessionId,
            UserId = document.UserId,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            ExpiresAt = MongoDates.FromUtc(document.ExpiresAt),
            SessionExpiresAt = MongoDates.FromUtc(document.SessionExpiresAt),
            RotatedAt = document.RotatedAt is { } rotated ? MongoDates.FromUtc(rotated) : null,
            RevokedAt = document.RevokedAt is { } revoked ? MongoDates.FromUtc(revoked) : null,
            RevokedReason = document.RevokedReason,
        };

    public void Dispose() => indexGate.Dispose();
}
