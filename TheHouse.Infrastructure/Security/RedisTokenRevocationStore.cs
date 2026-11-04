using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Infrastructure.Observability;

namespace TheHouse.Infrastructure.Security;

// NV-06: revocaciÃƒÂ³n en Redis compartido. `SET NX` con expiraciÃƒÂ³n
// es una operaciÃƒÂ³n atÃƒÂ³mica; la consulta usa `EXISTS`. Fallo cerrado:
// cualquier error de Redis produce DependencyUnavailableException,
// nunca un ÃƒÂ©xito silencioso ni un fallback local.
public sealed class RedisTokenRevocationStore(
    ConfigurationOptions? configuration) :
    ITokenRevocationStore,
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
