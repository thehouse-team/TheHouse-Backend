namespace TheHouse.Domain.Entities;

// NV-10: refresh token de una sesión. Solo se conserva el hash del
// valor entregado al cliente; el valor en claro nunca se persiste.
public sealed class RefreshToken
{
    public string TokenHash { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    // Vencimiento de este token (30 días o el fin de la sesión, lo que ocurra antes).
    public DateTimeOffset ExpiresAt { get; init; }

    // Vencimiento absoluto de la sesión (90 días desde el login).
    public DateTimeOffset SessionExpiresAt { get; init; }

    public DateTimeOffset? RotatedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string? RevokedReason { get; init; }

    public bool IsActive(DateTimeOffset now) =>
        RotatedAt is null &&
        RevokedAt is null &&
        ExpiresAt > now &&
        SessionExpiresAt > now;

    // Un token ya rotado que vuelve a presentarse indica que el valor
    // pudo ser robado: la sesión completa debe revocarse.
    public bool IsReuse =>
        RotatedAt is not null &&
        RevokedAt is null;
}
