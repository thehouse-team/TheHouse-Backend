using MongoDB.Driver;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Infrastructure.Persistence.Mongo.Documents;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// NV-11: `user_profiles` con índice único {userId}. Las escrituras son
// upserts atómicos: dos solicitudes simultáneas no crean dos documentos.
public sealed class MongoProfileRepository(IMongoDatabase database) : IProfileRepository, IDisposable
{
    private readonly UniqueUserIndex<UserProfileDocument> index =
        new(database.GetCollection<UserProfileDocument>("user_profiles"), "ux_user_profiles_user");

    private IMongoCollection<UserProfileDocument> Collection => index.Collection;

    public Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await Collection.Find(profile => profile.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null
                ? null
                : new UserProfile
                {
                    UserId = document.UserId,
                    Name = document.Name,
                    UniversityId = document.UniversityId,
                    UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
                };
        });

    public Task SaveNameAsync(string userId, string name, DateTimeOffset now, CancellationToken cancellationToken) =>
        UpsertAsync(userId, Builders<UserProfileDocument>.Update.Set(profile => profile.Name, name), now, cancellationToken);

    public Task SaveUniversityAsync(string userId, string universityId, DateTimeOffset now, CancellationToken cancellationToken) =>
        UpsertAsync(userId, Builders<UserProfileDocument>.Update.Set(profile => profile.UniversityId, universityId), now, cancellationToken);

    private Task UpsertAsync(
        string userId,
        UpdateDefinition<UserProfileDocument> change,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await index.EnsureAsync(cancellationToken);
            var update = Builders<UserProfileDocument>.Update.Combine(
                change,
                Builders<UserProfileDocument>.Update.Set(profile => profile.UpdatedAt, MongoDates.ToUtc(now)),
                Builders<UserProfileDocument>.Update.SetOnInsert(profile => profile.UserId, userId));
            await MongoCalls.RetryOnDuplicateAsync(() => Collection.UpdateOneAsync(
                profile => profile.UserId == userId,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken));
        });

    public void Dispose() => index.Dispose();
}

// NV-11: `user_preferences` con índice único {userId}; cada reemplazo
// incrementa `version` en la misma operación ($inc).
public sealed class MongoPreferencesRepository(IMongoDatabase database) : IPreferencesRepository, IDisposable
{
    private readonly UniqueUserIndex<UserPreferencesDocument> index =
        new(database.GetCollection<UserPreferencesDocument>("user_preferences"), "ux_user_preferences_user");

    private IMongoCollection<UserPreferencesDocument> Collection => index.Collection;

    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            var document = await Collection.Find(preferences => preferences.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return document is null ? null : Map(document);
        });

    public Task<UserPreferences> ReplaceAsync(UserPreferences preferences, CancellationToken cancellationToken) =>
        MongoCalls.Run(async () =>
        {
            await index.EnsureAsync(cancellationToken);
            var set = Builders<UserPreferencesDocument>.Update;
            var update = set.Combine(
                set.Set(document => document.BudgetMin, preferences.BudgetMin),
                set.Set(document => document.BudgetMax, preferences.BudgetMax),
                set.Set(document => document.Currency, preferences.Currency),
                set.Set(document => document.MaxDistanceKm, preferences.MaxDistanceKm),
                set.Set(document => document.EssentialServices, preferences.EssentialServices.ToList()),
                set.Set(document => document.LivingWith, preferences.LivingWith),
                set.Set(document => document.PlaceType, preferences.PlaceType),
                set.Set(document => document.Pets, preferences.Pets),
                set.Set(document => document.Transport, preferences.Transport),
                set.Set(document => document.PropertyTypes, preferences.PropertyTypes.ToList()),
                set.Set(document => document.QuickSetupCompleted, preferences.QuickSetupCompleted),
                set.Set(document => document.UpdatedAt, MongoDates.ToUtc(preferences.UpdatedAt ?? DateTimeOffset.UtcNow)),
                set.Inc(document => document.Version, 1L),
                set.SetOnInsert(document => document.UserId, preferences.UserId));

            var stored = await MongoCalls.RetryOnDuplicateAsync(() => Collection.FindOneAndUpdateAsync(
                document => document.UserId == preferences.UserId,
                update,
                new FindOneAndUpdateOptions<UserPreferencesDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken));
            return Map(stored);
        });

    private static UserPreferences Map(UserPreferencesDocument document) =>
        new()
        {
            UserId = document.UserId,
            BudgetMin = document.BudgetMin,
            BudgetMax = document.BudgetMax,
            Currency = document.Currency,
            MaxDistanceKm = document.MaxDistanceKm,
            EssentialServices = document.EssentialServices.ToArray(),
            LivingWith = document.LivingWith,
            PlaceType = document.PlaceType,
            Pets = document.Pets,
            Transport = document.Transport,
            PropertyTypes = document.PropertyTypes.ToArray(),
            QuickSetupCompleted = document.QuickSetupCompleted,
            Version = document.Version,
            UpdatedAt = MongoDates.FromUtc(document.UpdatedAt),
        };

    public void Dispose() => index.Dispose();
}

// Índice único {userId} creado una sola vez por proceso.
internal sealed class UniqueUserIndex<TDocument>(IMongoCollection<TDocument> collection, string name) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile bool ready;

    public IMongoCollection<TDocument> Collection => collection;

    public async Task EnsureAsync(CancellationToken cancellationToken)
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
                await collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<TDocument>(
                        Builders<TDocument>.IndexKeys.Ascending("userId"),
                        new CreateIndexOptions { Unique = true, Name = name }),
                    cancellationToken: cancellationToken);
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

internal static class MongoCalls
{
    public static async Task Run(Func<Task> operation)
    {
        try
        {
            await operation();
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

    public static async Task<T> Run<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
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

    // Dos upserts simultáneos del mismo usuario: uno inserta y el otro choca
    // con el índice único; al reintentar, ese segundo ya actualiza.
    public static async Task<T> RetryOnDuplicateAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (MongoException exception) when (IsDuplicateKey(exception))
        {
            return await operation();
        }
    }

    private static bool IsDuplicateKey(MongoException exception) =>
        exception is MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey } ||
        exception is MongoCommandException { Code: 11000 };
}

// Sin MongoDB habilitado, perfil y preferencias fallan cerrados (503).
public sealed class UnavailableProfileRepository : IProfileRepository
{
    public Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SaveNameAsync(string userId, string name, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task SaveUniversityAsync(string userId, string universityId, DateTimeOffset now, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}

public sealed class UnavailablePreferencesRepository : IPreferencesRepository
{
    public Task<UserPreferences?> GetAsync(string userId, CancellationToken cancellationToken) => throw new DependencyUnavailableException();

    public Task<UserPreferences> ReplaceAsync(UserPreferences preferences, CancellationToken cancellationToken) => throw new DependencyUnavailableException();
}
