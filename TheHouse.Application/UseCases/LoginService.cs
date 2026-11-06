using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;

namespace TheHouse.Application.UseCases;

public sealed class LoginService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    SessionService sessions)
{
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = EmailNormalizer.Normalize(request.Email);

        if (!EmailNormalizer.IsValid(email))
        {
            throw new RequestValidationException(
                "email",
                "email must be a valid address.");
        }

        PasswordPolicy.EnsureValidLoginPassword(request.Password);

        var password = request.Password!;

        var user = await userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (user is null)
        {
            passwordHasher.VerifyDummy(password);
            throw new InvalidCredentialsException();
        }

        var verification = passwordHasher.Verify(
            password,
            user.PasswordHash);

        if (!verification.IsValid)
        {
            throw new InvalidCredentialsException();
        }

        // NV-10: el login inicia una sesión con refresh token. Si no
        // puede guardarse, DependencyUnavailableException → 503 sin tokens.
        return await sessions.StartAsync(
            user,
            cancellationToken);
    }
}
