namespace TheHouse.Domain.Entities;

// NV-15 1.2.0: cuarto marcado como favorito por un usuario. Los favoritos
// creados en 1.1.0 que aún no se migran solo tienen `LegacyPropertyId`.
public sealed class Favorite
{
    public string UserId { get; init; } = string.Empty;

    public string RoomId { get; init; } = string.Empty;

    public string? LegacyPropertyId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
