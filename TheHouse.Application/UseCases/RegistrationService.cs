using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

public sealed class RegistrationService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = EmailNormalizer.Normalize(request.Email);
        if (!EmailNormalizer.IsValid(email))
        {
            throw new RequestValidationException("email", "email must be a valid address.");
        }

        PasswordPolicy.EnsureValidRegistrationPassword(request.Password);
        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password!),
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var created = await userRepository.CreateAsync(user, cancellationToken);
            return new RegisterResponse(created.Id, created.Email, created.CreatedAt);
        }
        catch (DuplicateEmailException)
        {
            throw new EmailConflictException();
        }
    }
}
