using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class UnavailableUniversityRepository :
    IUniversityRepository
{
    public Task<(
        IReadOnlyList<University> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new DependencyUnavailableException();
    }

    public Task<University?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new DependencyUnavailableException();
    }

    public Task<IReadOnlyList<University>> SearchByTextAsync(
        string normalizedQuery,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new DependencyUnavailableException();
    }

    public Task<IReadOnlyList<University>> SearchNearAsync(
        double latitude,
        double longitude,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new DependencyUnavailableException();
    }

    public Task UpsertSeedAsync(
        IReadOnlyCollection<University> universities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new DependencyUnavailableException();
    }
}
