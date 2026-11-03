using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User> CreateAsync(User user, CancellationToken cancellationToken);
}
