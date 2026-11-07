using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-12: `landlord_profiles` (único por usuario).
public interface ILandlordProfileRepository
{
    Task<LandlordProfile?> GetAsync(string userId, CancellationToken cancellationToken);

    // false si el usuario ya tenía perfil (índice único).
    Task<bool> CreateAsync(LandlordProfile profile, CancellationToken cancellationToken);
}

// NV-12: `buildings`. Las escrituras usan concurrencia optimista: solo
// aplican si `Version` sigue siendo `expectedVersion`.
public interface IBuildingRepository
{
    Task<Building> CreateAsync(Building building, CancellationToken cancellationToken);

    Task<Building?> GetAsync(string id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Building> Items, long TotalItems)> ListByOwnerAsync(string ownerId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Building>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    Task<long> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    Task<bool> ReplaceAsync(Building building, long expectedVersion, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken);
}

// NV-12: `rooms` con número único por edificio.
public interface IRoomRepository
{
    // Lanza ConflictException("room_number_taken") si algún número ya existe.
    Task<IReadOnlyList<Room>> CreateManyAsync(IReadOnlyList<Room> rooms, CancellationToken cancellationToken);

    Task<Room?> GetAsync(string id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Room> Items, long TotalItems)> ListByBuildingAsync(string buildingId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Room>> ListAllByBuildingAsync(string buildingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Room>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    // Lanza ConflictException("room_number_taken") si el número choca.
    Task<bool> ReplaceAsync(Room room, long expectedVersion, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken);

    Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken);

    // Pasa a `to` (versión + 1) los cuartos del edificio en `from`.
    Task SetStatusByBuildingAsync(string buildingId, RoomStatus from, RoomStatus to, DateTimeOffset now, CancellationToken cancellationToken);
}
