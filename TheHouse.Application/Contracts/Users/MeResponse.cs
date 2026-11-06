namespace TheHouse.Application.Contracts.Users;

// NV-10: GET /users/me devuelve solo campos reales del dominio; nuevos
// campos se agregan de forma aditiva cuando existan.
public sealed record MeResponse(string Id, string Email, DateTimeOffset CreatedAt);
