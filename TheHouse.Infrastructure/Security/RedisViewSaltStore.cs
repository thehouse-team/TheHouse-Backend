using System.Security.Cryptography;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Analytics;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Security;

// NV-25: sal aleatoria del día para `actorHash`. La primera réplica que la
// necesita la crea con `SET NX` y 48 h de expiración; las demás la leen. Al
// vencer se pierde, así que los hashes de días distintos no se pueden unir.
public sealed class RedisViewSaltStore(ConfigurationOptions? configuration) : IViewSaltStore, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private readonly Lazy<Task<ConnectionMultiplexer>> connection =
        new(() =>
            configuration is null
                ? Task.FromException<ConnectionMultiplexer>(new DependencyUnavailableException())
                : ConnectionMultiplexer.ConnectAsync(configuration));

    public static string Key(string day) => "thehouse:u2:analytics:view-salt:v1:" + day;

    public async Task<byte[]> GetDailySaltAsync(string day, CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        var timeout = TimeSpan.FromMilliseconds(configuration.AsyncTimeout);
        try
        {
            var client = await connection.Value.WaitAsync(timeout, cancellationToken);
            var database = client.GetDatabase();
            var key = Key(day);
            await database.StringSetAsync(key, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), Lifetime, When.NotExists).WaitAsync(timeout, cancellationToken);
            var value = await database.StringGetAsync(key).WaitAsync(timeout, cancellationToken);
            return value.HasValue ? Convert.FromBase64String(value.ToString()) : throw new DependencyUnavailableException();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or FormatException)
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
}
