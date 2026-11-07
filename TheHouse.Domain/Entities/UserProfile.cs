namespace TheHouse.Domain.Entities;

// NV-11: perfil propio del estudiante; un documento por usuario.
public sealed class UserProfile
{
    public string UserId { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? UniversityId { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
