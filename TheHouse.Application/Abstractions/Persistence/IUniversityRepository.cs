using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

public interface IUniversityRepository
{
    Task<(
        IReadOnlyList<University> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<University?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken);

    // NV-11: coincidencia parcial sobre `searchKey` (texto ya normalizado).
    Task<IReadOnlyList<University>> SearchByTextAsync(
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken);

    // NV-11: universidades con ubicación, de la más cercana a la más lejana.
    Task<IReadOnlyList<University>> SearchNearAsync(
        double latitude,
        double longitude,
        int limit,
        CancellationToken cancellationToken);

    Task UpsertSeedAsync(
        IReadOnlyCollection<University> universities,
        CancellationToken cancellationToken);
}
