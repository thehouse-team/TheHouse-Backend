using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Infrastructure.Persistence.Mongo;

public sealed class UnavailableUserRepository : IUserRepository, IUserAccountRepository
{
    public Task<User?> GetByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }

    public Task<User?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }

    public Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new DependencyUnavailableException();
    }

    public Task<bool> SetLegalAsync(string userId, LegalAcceptance legal, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();

    public Task DeleteAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException();
}
