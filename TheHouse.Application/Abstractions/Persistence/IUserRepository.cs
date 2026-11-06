using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    // NV-10: búsqueda por identificador; un id con formato inválido
    // se trata como inexistente (null), nunca como error.
    Task<User?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken);

    Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken);
}
