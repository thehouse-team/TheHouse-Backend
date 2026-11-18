namespace TheHouse.Application.Contracts.Users;

// NV-10: GET /users/me devuelve solo campos reales del dominio; nuevos
// campos se agregan de forma aditiva cuando existan. 1.2.0 agrega la
// aceptación legal y si corresponde a las versiones vigentes.
public sealed record MeResponse(
    string Id,
    string Email,
    DateTimeOffset CreatedAt,
    LegalAcceptanceResponse? Legal = null,
    bool LegalUpToDate = false);
