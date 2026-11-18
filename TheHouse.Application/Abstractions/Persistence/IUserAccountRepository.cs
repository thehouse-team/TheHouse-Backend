using TheHouse.Domain.Entities;

namespace TheHouse.Application.Abstractions.Persistence;

// NV-10 1.2.0: cambios a la cuenta propia (aceptación legal y eliminación).
// Separado de IUserRepository para no cambiar el contrato que usan login,
// registro y refresh.
public interface IUserAccountRepository
{
    // Devuelve false si la cuenta ya no existe.
    Task<bool> SetLegalAsync(string userId, LegalAcceptance legal, CancellationToken cancellationToken);

    // Guarda la marca en `deleted_accounts` (solo id y fecha) y borra la
    // cuenta (correo y hash de contraseña). Idempotente.
    Task DeleteAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken);
}
