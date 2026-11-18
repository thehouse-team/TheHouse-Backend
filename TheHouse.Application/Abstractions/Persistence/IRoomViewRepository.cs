using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-25: `room_views` (módulo Analytics). Único `{ roomId, buildingId, day,
// actorHash }`; `{ ownerId, day }` para las consultas; TTL de 400 días.
public interface IRoomViewRepository
{
    // false si esa persona ya vio ese cuarto ese día (idempotente entre réplicas).
    Task<bool> AddAsync(RoomView view, CancellationToken cancellationToken);

    // Vistas por cuarto y día del dueño, con `fromDay` y `toDay` incluidos.
    Task<IReadOnlyList<RoomViewCount>> CountAsync(string ownerId, string fromDay, string toDay, CancellationToken cancellationToken);

    // NV-10 1.2.0: vistas de los cuartos de un dueño que eliminó su cuenta.
    Task DeleteByOwnerAsync(string ownerId, CancellationToken cancellationToken);
}
