using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class MongoUserRepository(IMongoDatabase database) : IUserRepository, IDisposable
{
    private readonly SemaphoreSlim indexGate = new(1, 1);
    private volatile bool indexReady;

    private IMongoCollection<UserDocument> Collection =>
        database.GetCollection<UserDocument>("users");

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
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

            await Collection.Indexes.CreateOneAsync(
                new CreateIndexModel<UserDocument>(
                    Builders<UserDocument>.IndexKeys.Ascending(user => user.Email),
                    new CreateIndexOptions
                    {
                        Unique = true,
                        Name = "ux_users_email"
                    }),
                cancellationToken: cancellationToken);

            indexReady = true;
        }
        finally
        {
            indexGate.Release();
        }
    }

    public async Task<User?> GetByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        if (normalizedEmail != EmailNormalizer.Normalize(normalizedEmail) ||
            !EmailNormalizer.IsValid(normalizedEmail))
        {
            throw new RequestValidationException("email", "Invalid email.");
        }

        try
        {
            var document = await Collection
                .Find(user => user.Email == normalizedEmail)
                .FirstOrDefaultAsync(cancellationToken);

            return document is null ? null : Map(document);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    public async Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (user.Email != EmailNormalizer.Normalize(user.Email) ||
            !EmailNormalizer.IsValid(user.Email))
        {
            throw new RequestValidationException("email", "Invalid email.");
        }

        var document = new UserDocument
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            CreatedAt = MongoDates.ToUtc(user.CreatedAt),
            UpdatedAt = MongoDates.ToUtc(user.UpdatedAt),
        };

        try
        {
            await EnsureIndexAsync(cancellationToken);

            await Collection.InsertOneAsync(
                document,
                cancellationToken: cancellationToken);

            return Map(document);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateEmailException();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            throw MongoFailure.Unavailable();
        }
    }

    private static User Map(UserDocument document) =>
        new()
        {
            Id = document.Id.ToString(),
            Email = document.Email,
            PasswordHash = document.PasswordHash,
            CreatedAt = MongoDates.FromUtc(document.CreatedAt),
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => indexGate.Dispose();
}
