using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Favorites;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-15 1.2.0: favoritos del usuario autenticado sobre cuartos publicados.
// Transición: los ids de las 12 propiedades de NV-07 se traducen a su
// cuarto. Los favoritos de cuartos no publicados se ocultan sin borrarse.
public sealed class FavoriteService(
    IFavoriteRepository favorites,
    RoomCatalog catalog,
    TimeProvider timeProvider)
{
    public const int MaximumFavorites = 200;

    public async Task AddAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var room = await catalog.FindAsync(RequireId(id), cancellationToken);
        if (room is null || !room.IsPublished)
        {
            throw new NotFoundException("room");
        }

        // Idempotente: si ya es favorito no se cuenta contra el límite.
        if (await favorites.ExistsAsync(userId, room.Room.Id, room.LegacyPropertyId, cancellationToken))
        {
            return;
        }

        if (await favorites.CountAsync(userId, cancellationToken) >= MaximumFavorites)
        {
            throw new ConflictException("favorites_limit");
        }

        await favorites.AddAsync(
            new Favorite { UserId = userId, RoomId = room.Room.Id, CreatedAt = timeProvider.GetUtcNow() },
            cancellationToken);
    }

    public async Task RemoveAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var requested = RequireId(id);
        var room = await catalog.FindAsync(requested, cancellationToken);
        await favorites.RemoveAsync(
            userId,
            room?.Room.Id ?? requested,
            room?.LegacyPropertyId ?? requested,
            cancellationToken);
    }

    public async Task<FavoriteListResponse> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var visible = await VisibleAsync(userId, cancellationToken);

        var items = visible
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FavoriteItemResponse(
                item.Room.Room.Id,
                item.Room.Building.Id,
                item.Room.Title,
                item.Room.Room.Price ?? 0,
                "MXN",
                item.Room.Room.Type,
                item.Room.Room.Bathroom,
                item.Room.Building.Address.City,
                item.Room.ApproxLocation,
                item.Room.Building.Demo,
                item.Favorite.CreatedAt,
                item.Room.LegacyPropertyId))
            .ToArray();

        return new FavoriteListResponse(items, page, pageSize, visible.Count, PropertyQueryValidation.CalculateTotalPages(visible.Count, pageSize));
    }

    public async Task<FavoriteIdsResponse> ListIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var visible = await VisibleAsync(userId, cancellationToken);
        return new FavoriteIdsResponse(
            visible.Select(item => item.Room.Room.Id).ToArray(),
            visible.Select(item => item.Room.LegacyPropertyId).OfType<string>().ToArray());
    }

    // Favoritos cuyo cuarto está publicado, más recientes primero. Los de
    // 1.1.0 aún sin migrar se traducen con el mapeo de demostración.
    private async Task<List<(Favorite Favorite, CatalogRoom Room)>> VisibleAsync(string userId, CancellationToken cancellationToken)
    {
        var all = await favorites.ListAllAsync(userId, cancellationToken);
        var legacy = all.Any(favorite => favorite.RoomId.Length == 0)
            ? await catalog.LegacyMapAsync(cancellationToken)
            : new Dictionary<string, string>();

        var roomIds = all
            .Select(favorite => favorite.RoomId.Length > 0 ? favorite.RoomId : legacy.GetValueOrDefault(favorite.LegacyPropertyId ?? string.Empty) ?? string.Empty)
            .ToArray();
        var rooms = await catalog.LoadAsync(roomIds, cancellationToken);

        var visible = new List<(Favorite, CatalogRoom)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < all.Count; index++)
        {
            if (rooms.TryGetValue(roomIds[index], out var room) && room.IsPublished && seen.Add(room.Room.Id))
            {
                visible.Add((all[index], room));
            }
        }

        return visible;
    }

    private static string RequireId(string? id) =>
        PropertyQueryValidation.IsObjectId(id)
            ? id!.ToLowerInvariant()
            : throw new RequestValidationException("roomId", "roomId must be a 24-character hexadecimal ObjectId.");
}

// NV-15/NV-17 1.2.0: migración autorizada de favoritos y visitas a cuartos.
public sealed class RoomMigrationService(RoomCatalog catalog, IFavoriteRepository favorites, IVisitRepository visits)
{
    public async Task<(MigrationCounts Favorites, MigrationCounts Visits)> MigrateAsync(CancellationToken cancellationToken)
    {
        var map = await catalog.LegacyMapAsync(cancellationToken);
        if (map.Count == 0)
        {
            throw new InvalidOperationException("Room migration requires the demo room seed (--seed-demo-rooms) first.");
        }

        return (await favorites.MigrateLegacyAsync(map, cancellationToken), await visits.MigrateLegacyAsync(map, cancellationToken));
    }
}

public sealed class RoomMigrationOptions
{
    public bool Enabled { get; init; }
}
