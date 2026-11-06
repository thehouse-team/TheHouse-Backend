using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;

namespace TheHouse.Application.UseCases;

// NV-10: cuenta propia. Solo se consulta el usuario del `sub` del token
// validado; no existe forma de pedir otra cuenta (sin BOLA por diseño).
public sealed class AccountService(IUserRepository users)
{
    public async Task<MeResponse> GetMeAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await users.GetByIdAsync(userId, cancellationToken);

        // Token válido de un usuario que ya no existe: 401, sin revelar
        // la diferencia con un token inválido.
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        return new MeResponse(user.Id, user.Email, user.CreatedAt);
    }
}
