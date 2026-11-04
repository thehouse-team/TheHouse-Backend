using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;

namespace TheHouse.Application.UseCases;

public sealed class LoginService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService)
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

        var token = jwtTokenService.CreateAccessToken(user);

        return new LoginResponse(
            token.Token,
            token.TokenType,
            token.ExpiresIn);
    }
}
