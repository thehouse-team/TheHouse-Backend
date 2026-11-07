namespace TheHouse.Domain.Entities;

// NV-15: propiedad marcada como favorita por un usuario.
public sealed class Favorite
{
    public string UserId { get; init; } = string.Empty;

    public string PropertyId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
