using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class UnavailablePropertyRepository :
    IPropertyRepository
{
    public Task<(
        IReadOnlyList<Property> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }

    public Task<Property?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }

    public Task UpsertSeedAsync(
        IReadOnlyCollection<Property> properties,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }
}
