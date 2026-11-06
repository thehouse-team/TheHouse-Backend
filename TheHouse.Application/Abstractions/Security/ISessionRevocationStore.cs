namespace TheHouse.Application.Abstractions.Security;

// NV-10: revocación compartida de sesiones (`sid`) para rechazar de
// inmediato los access tokens de una sesión revocada en cualquier
// réplica. Sin caché ni fallback local: fallo cerrado.
public interface ISessionRevocationStore
{
    Task RevokeSessionAsync(
        string sessionId,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task<bool> IsSessionRevokedAsync(
        string sessionId,
        CancellationToken cancellationToken);
}
