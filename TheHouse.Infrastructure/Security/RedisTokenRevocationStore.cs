using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Infrastructure.Observability;

namespace TheHouse.Infrastructure.Security;

// NV-06: revocación en Redis compartido. `SET NX` con expiración
// es una operación atómica; la consulta usa `EXISTS`. Fallo cerrado:
// cualquier error de Redis produce DependencyUnavailableException,
// nunca un éxito silencioso ni un fallback local.
public sealed class RedisTokenRevocationStore(
    ConfigurationOptions? configuration) :
    ITokenRevocationStore,
    ISessionRevocationStore,
    IDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> connection =
        new(() =>
            configuration is null
                ? Task.FromException<ConnectionMultiplexer>(
                    new DependencyUnavailableException())
                : ConnectionMultiplexer.ConnectAsync(configuration));

    public async Task RevokeAsync(
        string jti,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var activity =
                TelemetrySources.Redis.StartActivity(
                    "redis.token_revoke",
                    System.Diagnostics.ActivityKind.Client);

            activity?.SetTag(
                "db.system.name",
                "redis");

            var timeout =
                TimeSpan.FromMilliseconds(configuration.AsyncTimeout);

            var client = await connection.Value.WaitAsync(
                timeout,
                cancellationToken);

            await client
                .GetDatabase()
                .StringSetAsync(
                    new RedisKey(RevocationKeys.ForJti(jti)),
                    new RedisValue("1"),
                    ttl,
                    When.NotExists)
                .WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    public async Task<bool> IsRevokedAsync(
        string jti,
        CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var activity =
                TelemetrySources.Redis.StartActivity(
                    "redis.token_revocation_check",
                    System.Diagnostics.ActivityKind.Client);

            activity?.SetTag(
                "db.system.name",
                "redis");

            var timeout =
                TimeSpan.FromMilliseconds(configuration.AsyncTimeout);

            var client = await connection.Value.WaitAsync(
                timeout,
                cancellationToken);

            var exists = await client
                .GetDatabase()
                .KeyExistsAsync(
                    RevocationKeys.ForJti(jti))
                .WaitAsync(timeout, cancellationToken);

            return exists;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    // NV-10: revocación de sesión (`sid`) con la misma semántica de
    // `SET NX` + TTL y fallo cerrado que la revocación por `jti`.
    public async Task RevokeSessionAsync(
        string sessionId,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (!SessionTokens.IsCanonicalSessionId(sessionId))
        {
            throw new ArgumentException(
                "Invalid session identifier.",
                nameof(sessionId));
        }

        await ExecuteAsync(
            "redis.session_revoke",
            database => database.StringSetAsync(
                new RedisKey(SessionTokens.RevokedSessionKey(sessionId)),
                new RedisValue("1"),
                ttl,
                When.NotExists),
            cancellationToken);
    }

    public async Task<bool> IsSessionRevokedAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (!SessionTokens.IsCanonicalSessionId(sessionId))
        {
            throw new ArgumentException(
                "Invalid session identifier.",
                nameof(sessionId));
        }

        return await ExecuteAsync(
            "redis.session_revocation_check",
            database => database.KeyExistsAsync(
                new RedisKey(SessionTokens.RevokedSessionKey(sessionId))),
            cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        string activityName,
        Func<IDatabase, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var activity =
                TelemetrySources.Redis.StartActivity(
                    activityName,
                    System.Diagnostics.ActivityKind.Client);

            activity?.SetTag(
                "db.system.name",
                "redis");

            var timeout =
                TimeSpan.FromMilliseconds(configuration.AsyncTimeout);

            var client = await connection.Value.WaitAsync(
                timeout,
                cancellationToken);

            return await operation(client.GetDatabase())
                .WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    public void Dispose()
    {
        if (!connection.IsValueCreated)
        {
            return;
        }

        _ = connection.Value.ContinueWith(
            task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    task.Result.Dispose();
                }
                else
                {
                    _ = task.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
