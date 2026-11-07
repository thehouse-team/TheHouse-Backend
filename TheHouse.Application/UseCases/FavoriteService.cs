using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Favorites;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-15: favoritos del usuario autenticado sobre las propiedades de NV-07.
// El usuario siempre viene del token (`sub`); nunca del cliente.
public sealed class FavoriteService(
    IFavoriteRepository favorites,
    IPropertyRepository properties,
    TimeProvider timeProvider)
{
    public const int MaximumFavorites = 200;

    public async Task AddAsync(string userId, string? propertyId, CancellationToken cancellationToken)
    {
        var id = RequirePropertyId(propertyId);

        if (await properties.GetByIdAsync(id, cancellationToken) is null)
        {
            throw new NotFoundException("property");
        }

        // Idempotente: si ya es favorita no se cuenta contra el límite.
        if (await favorites.ExistsAsync(userId, id, cancellationToken))
        {
            return;
        }

        if (await favorites.CountAsync(userId, cancellationToken) >= MaximumFavorites)
        {
            throw new ConflictException("favorites_limit");
        }

        await favorites.AddAsync(
            new Favorite { UserId = userId, PropertyId = id, CreatedAt = timeProvider.GetUtcNow() },
            cancellationToken);
    }

    public Task RemoveAsync(string userId, string? propertyId, CancellationToken cancellationToken) =>
        favorites.RemoveAsync(userId, RequirePropertyId(propertyId), cancellationToken);

    public async Task<FavoriteListResponse> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);

        var (items, total) = await favorites.ListAsync(userId, page, pageSize, cancellationToken);
        var found = await LoadAsync(items.Select(item => item.PropertyId).ToArray(), cancellationToken);
        var missing = await CleanOrphansAsync(userId, items.Select(item => item.PropertyId), found, cancellationToken);

        var result = items
            .Where(item => found.ContainsKey(item.PropertyId))
            .Select(item =>
            {
                var property = found[item.PropertyId];
                return new FavoriteItemResponse(
                    property.Id,
                    property.Title,
                    property.Price,
                    property.Currency,
                    property.City,
                    property.UniversityId,
                    property.Bedrooms,
                    property.Bathrooms,
                    property.AreaM2,
                    item.CreatedAt);
            })
            .ToArray();

        var totalItems = Math.Max(0, total - missing);
        return new FavoriteListResponse(
            result,
            page,
            pageSize,
            totalItems,
            PropertyQueryValidation.CalculateTotalPages(totalItems, pageSize));
    }

    public async Task<FavoriteIdsResponse> ListIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var ids = await favorites.ListPropertyIdsAsync(userId, MaximumFavorites, cancellationToken);
        var found = await LoadAsync(ids, cancellationToken);
        await CleanOrphansAsync(userId, ids, found, cancellationToken);
        return new FavoriteIdsResponse(ids.Where(found.ContainsKey).ToArray());
    }

    private async Task<Dictionary<string, Property>> LoadAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, Property>(StringComparer.Ordinal);
        }

        var loaded = await properties.GetByIdsAsync(ids, cancellationToken);
        return loaded.ToDictionary(property => property.Id, StringComparer.Ordinal);
    }

    // Favoritos de propiedades que ya no existen: se omiten y se limpian.
    private async Task<int> CleanOrphansAsync(
        string userId,
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, Property> found,
        CancellationToken cancellationToken)
    {
        var orphans = ids.Where(id => !found.ContainsKey(id)).ToArray();
        if (orphans.Length > 0)
        {
            await favorites.RemoveManyAsync(userId, orphans, cancellationToken);
        }

        return orphans.Length;
    }

    private static string RequirePropertyId(string? propertyId)
    {
        if (!PropertyQueryValidation.IsObjectId(propertyId))
        {
            throw new RequestValidationException(
                "propertyId",
                "propertyId must be a 24-character hexadecimal ObjectId.");
        }

        return propertyId!.ToLowerInvariant();
    }
}
