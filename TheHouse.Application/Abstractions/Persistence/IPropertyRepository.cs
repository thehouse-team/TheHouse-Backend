using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

public interface IPropertyRepository
{
    Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Property?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken);
}
