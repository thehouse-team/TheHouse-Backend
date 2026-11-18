namespace TheHouse.Domain.Entities;

// NV-25: una vista del detalle de un cuarto por persona y día. `ActorHash`
// usa una sal aleatoria del día que se descarta: no identifica a nadie ni
// permite seguir a la misma persona entre días.
public sealed class RoomView
{
    public string RoomId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    // `YYYY-MM-DD` en la zona de la Ciudad de México.
    public string Day { get; init; } = string.Empty;

    public string ActorHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record RoomViewCount(string RoomId, string BuildingId, string Day, int Count);
