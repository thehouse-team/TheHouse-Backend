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

    // NV-15/NV-17: lectura por lotes con proyección pública; ids inválidos
    // o inexistentes simplemente no aparecen en el resultado.
    Task<IReadOnlyList<Property>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken);

    Task UpsertSeedAsync(
        IReadOnlyCollection<Property> properties,
        CancellationToken cancellationToken);
}
