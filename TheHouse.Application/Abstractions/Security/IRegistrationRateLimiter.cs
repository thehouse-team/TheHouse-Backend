namespace TheHouse.Application.Abstractions.Security;

public interface IRegistrationRateLimiter
{
    Task<bool> AllowAsync(string clientAddress, CancellationToken cancellationToken);
}
