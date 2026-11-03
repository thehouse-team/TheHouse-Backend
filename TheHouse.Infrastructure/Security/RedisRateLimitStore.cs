using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.Security;

public sealed class RedisRateLimitStore(
    ConfigurationOptions? configuration) :
    IRateLimitStore,
    IDisposable
{
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { count, ttl }
        """;

    private readonly Lazy<Task<ConnectionMultiplexer>> connection =
        new(() =>
            configuration is null
                ? Task.FromException<ConnectionMultiplexer>(
                    new DependencyUnavailableException())
                : ConnectionMultiplexer.ConnectAsync(configuration));

    public async Task<RateLimitDecision> ConsumeAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (configuration is null)
        {
            throw new DependencyUnavailableException();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeout =
                TimeSpan.FromMilliseconds(configuration.AsyncTimeout);

            var client = await connection.Value.WaitAsync(
                timeout,
                cancellationToken);

            var result = await client
                .GetDatabase()
                .ScriptEvaluateAsync(
                    Script,
                    [new RedisKey(key)],
                    [new RedisValue(((long)window.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture))])
                .WaitAsync(timeout, cancellationToken);

            var values = (RedisResult[]?)result;

            if (values is null || values.Length != 2)
            {
                throw new RedisException(
                    "Unexpected rate limit response.");
            }

            var count = (long)values[0];
            var ttlMilliseconds = (long)values[1];

            var retryAfter = ttlMilliseconds > 0
                ? TimeSpan.FromMilliseconds(ttlMilliseconds)
                : window;

            return new RateLimitDecision(
                count <= limit,
                count,
                retryAfter);
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
