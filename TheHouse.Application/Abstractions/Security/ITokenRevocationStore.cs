namespace TheHouse.Application.Abstractions.Security;

// NV-06: almacén compartido de revocación de JWT por `jti`.
// La ausencia de fallback local y el fallo cerrado son obligatorios.
public interface ITokenRevocationStore
{
    Task RevokeAsync(
        string jti,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task<bool> IsRevokedAsync(
        string jti,
        CancellationToken cancellationToken);
}
