using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

// Sin MongoDB habilitado, favoritos y visitas fallan cerrados (503).
public sealed class UnavailableFavoriteRepository : IFavoriteRepository
{
    public Task<bool> AddAsync(Favorite favorite, CancellationToken cancellationToken) => Fail<bool>();

    public Task RemoveAsync(string userId, string propertyId, CancellationToken cancellationToken) => Fail<bool>();

    public Task RemoveManyAsync(string userId, IReadOnlyCollection<string> propertyIds, CancellationToken cancellationToken) => Fail<bool>();

    public Task<bool> ExistsAsync(string userId, string propertyId, CancellationToken cancellationToken) => Fail<bool>();

    public Task<long> CountAsync(string userId, CancellationToken cancellationToken) => Fail<long>();

    public Task<(IReadOnlyList<Favorite> Items, long TotalItems)> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken) =>
        Fail<(IReadOnlyList<Favorite>, long)>();

    public Task<IReadOnlyList<string>> ListPropertyIdsAsync(string userId, int limit, CancellationToken cancellationToken) =>
        Fail<IReadOnlyList<string>>();

    private static Task<T> Fail<T>() => throw new DependencyUnavailableException();
}

public sealed class UnavailableVisitRepository : IVisitRepository
{
    public Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken) => Fail<Visit>();

    public Task<Visit?> GetAsync(string id, CancellationToken cancellationToken) => Fail<Visit?>();

    public Task UpdateAsync(Visit visit, CancellationToken cancellationToken) => Fail<bool>();

    public Task<IReadOnlyList<Visit>> ListActiveAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Fail<IReadOnlyList<Visit>>();

    public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListAsync(string userId, VisitStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
        Fail<(IReadOnlyList<Visit>, long)>();

    public Task<VisitIdempotencyRecord?> FindIdempotencyAsync(string userId, string key, CancellationToken cancellationToken) =>
        Fail<VisitIdempotencyRecord?>();

    public Task SaveIdempotencyAsync(VisitIdempotencyRecord record, CancellationToken cancellationToken) => Fail<bool>();

    private static Task<T> Fail<T>() => throw new DependencyUnavailableException();
}
