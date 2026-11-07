namespace TheHouse.Application.Abstractions.Security;

// NV-17: lock distribuido (Redis) para serializar operaciones de un mismo
// usuario entre réplicas (constitution §14). Devuelve null si el lock ya
// está tomado; el lock expira solo aunque la réplica falle.
public interface IUserLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}
