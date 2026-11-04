namespace TheHouse.Application.Abstractions.Health;

public interface IMongoHealthProbe
{
    ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken);
}

public interface IRedisHealthProbe
{
    ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken);
}
