using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Security;

public sealed class RedisRegistrationRateLimiter(ConfigurationOptions? configuration) : IRegistrationRateLimiter, IDisposable
{
    // INCR y expiración son una operación atómica compartida; no existe fallback en memoria.
    private const string Script = "local n=redis.call('INCR',KEYS[1]); if n==1 then redis.call('PEXPIRE',KEYS[1],60000) end; if n<=10 then return 1 else return 0 end";
    private readonly Lazy<Task<ConnectionMultiplexer>> connection = new(() =>
        configuration is null ? Task.FromException<ConnectionMultiplexer>(new DependencyUnavailableException())
            : ConnectionMultiplexer.ConnectAsync(configuration));

    public static string KeyFor(string clientAddress) =>
        "thehouse:u2:registration:rate:v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientAddress)));

    public async Task<bool> AllowAsync(string clientAddress, CancellationToken cancellationToken)
    {
        if (configuration is null) throw new DependencyUnavailableException();
        try
        {
            var timeout = TimeSpan.FromMilliseconds(configuration.AsyncTimeout);
            var client = await connection.Value.WaitAsync(timeout, cancellationToken);
            var result = await client.GetDatabase().ScriptEvaluateAsync(Script,
                [new RedisKey(KeyFor(clientAddress))]).WaitAsync(timeout, cancellationToken);
            return (long)result == 1;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new DependencyUnavailableException();
        }
    }

    public void Dispose()
    {
        if (!connection.IsValueCreated) return;
        // No bloquear el shutdown ni dejar una conexión tardía sin liberar.
        _ = connection.Value.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully) task.Result.Dispose();
            else _ = task.Exception;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
