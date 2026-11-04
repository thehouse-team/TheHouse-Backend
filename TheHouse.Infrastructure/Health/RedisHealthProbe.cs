using StackExchange.Redis;
using TheHouse.Application.Abstractions.Health;

namespace TheHouse.Infrastructure.Health;

public sealed class RedisHealthProbe(
    ConfigurationOptions configuration) :
    IRedisHealthProbe,
    IDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>>
        connection =
            new(() =>
                ConnectionMultiplexer.ConnectAsync(
                    configuration));

    public async ValueTask<bool> IsReadyAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timeout =
                TimeSpan.FromMilliseconds(
                    configuration.AsyncTimeout);

            var client =
                await connection.Value.WaitAsync(
                    timeout,
                    cancellationToken);

            _ = await client
                .GetDatabase()
                .PingAsync()
                .WaitAsync(
                    timeout,
                    cancellationToken);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is
                  RedisException or
                  TimeoutException)
        {
            return false;
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
