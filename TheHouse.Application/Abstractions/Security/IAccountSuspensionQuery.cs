namespace TheHouse.Application.Abstractions.Security;

// NV-21: Identity pregunta a Moderation si una cuenta está suspendida
// (login y refresh responden 403 `account_suspended`).
public interface IAccountSuspensionQuery
{
    Task<bool> IsSuspendedAsync(string userId, CancellationToken cancellationToken);
}
