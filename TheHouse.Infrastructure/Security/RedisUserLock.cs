using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Security;

// NV-17: lock distribuido en Redis. `SET NX PX` con un token aleatorio; la
// liberación solo borra la llave si el token coincide (script Lua), para
// no soltar un lock que ya expiró y tomó otra réplica. Fallo cerrado.
public sealed class RedisUserLock(ConfigurationOptions? configuration) : IUserLock, IDisposable
{
    private const string ReleaseScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

    private readonly Lazy<Task<ConnectionMultiplexer>> connection =
        new(() =>
            configuration is null
                ? Task.FromException<ConnectionMultiplexer>(new DependencyUnavailableException())
                : ConnectionMultiplexer.ConnectAsync(configuration));

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var database = await DatabaseAsync(cancellationToken);
        var token = Guid.NewGuid().ToString("N");

        try
        {
            var acquired = await database
                .StringSetAsync(key, token, ttl, When.NotExists)
                .WaitAsync(Timeout, cancellationToken);
            return acquired ? new Handle(database, key, token) : null;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    private TimeSpan Timeout =>
        TimeSpan.FromMilliseconds(configuration?.AsyncTimeout ?? 5000);

    private async Task<IDatabase> DatabaseAsync(CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        try
        {
            var client = await connection.Value.WaitAsync(Timeout, cancellationToken);
            return client.GetDatabase();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    public void Dispose()
    {
        if (connection.IsValueCreated && connection.Value.IsCompletedSuccessfully)
        {
            connection.Value.Result.Dispose();
        }
    }

    private sealed class Handle(IDatabase database, string key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await database.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                // Si no se puede liberar, el TTL lo libera; no se oculta otra falla.
            }
        }
    }
}
