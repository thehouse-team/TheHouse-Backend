using TheHouse.Application.Abstractions.Health;

namespace TheHouse.Infrastructure.Health;

// Fail-closed: habilitar opciones nunca simula una conexión saludable.
public sealed class UnconfiguredMongoHealthProbe : IMongoHealthProbe
{
    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}

public sealed class UnconfiguredRedisHealthProbe : IRedisHealthProbe
{
    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}
