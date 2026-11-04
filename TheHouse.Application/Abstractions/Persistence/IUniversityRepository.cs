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

    Task UpsertSeedAsync(
        IReadOnlyCollection<University> universities,
        CancellationToken cancellationToken);
}
