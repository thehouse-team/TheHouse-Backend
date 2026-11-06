using System.Security.Claims;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.UnitTests;

public sealed class LoginServiceTests
{
    [Fact]
    public async Task Unknown_user_executes_dummy_hash_and_returns_generic_error()
    {
        var repository = new LoginRepository();
        var hasher = new LoginHasher();
        var jwt = new LoginJwt();
        var service = new LoginService(repository, hasher, Sessions(repository, jwt));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(
                new LoginRequest
                {
                    Email = " Test@Example.COM ",
                    Password = "fixture-value"
                },
                default));

        Assert.Equal("test@example.com", repository.RequestedEmail);
        Assert.Equal(1, hasher.DummyCalls);
        Assert.Equal(0, jwt.CreateCalls);
    }

    [Fact]
    public async Task Wrong_password_returns_same_generic_error()
    {
        var repository = new LoginRepository
        {
            User = User()
        };

        var hasher = new LoginHasher
        {
            Verification = new PasswordVerification(false)
        };

        var jwt = new LoginJwt();
        var service = new LoginService(repository, hasher, Sessions(repository, jwt));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(
                new LoginRequest
                {
                    Email = "test@example.com",
                    Password = "wrong"
                },
                default));

        Assert.Equal(0, hasher.DummyCalls);
        Assert.Equal(1, hasher.VerifyCalls);
        Assert.Equal(0, jwt.CreateCalls);
    }

    [Fact]
    public async Task Valid_credentials_return_access_and_refresh_token_contract()
    {
        var repository = new LoginRepository
        {
            User = User()
        };

        var hasher = new LoginHasher
        {
            Verification = new PasswordVerification(true)
        };

        var jwt = new LoginJwt();
        var service = new LoginService(repository, hasher, Sessions(repository, jwt));

        var response = await service.LoginAsync(
            new LoginRequest
            {
                Email = "test@example.com",
                Password = "fixture-value"
            },
            default);

        Assert.Equal("fixture-token", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(900, response.ExpiresIn);
        Assert.True(SessionTokens.IsWellFormedRefreshToken(response.RefreshToken));
        Assert.Equal(1, jwt.CreateCalls);
    }

    [Fact]
    public async Task Invalid_email_does_not_query_repository_or_hash_password()
    {
        var repository = new LoginRepository();
        var hasher = new LoginHasher();
        var jwt = new LoginJwt();

        var service = new LoginService(repository, hasher, Sessions(repository, jwt));

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.LoginAsync(
                new LoginRequest
                {
                    Email = "invalid",
                    Password = "fixture-value"
                },
                default));

        Assert.Null(repository.RequestedEmail);
        Assert.Equal(0, hasher.VerifyCalls);
        Assert.Equal(0, hasher.DummyCalls);
        Assert.Equal(0, jwt.CreateCalls);
    }

    private static User User() =>
        new()
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = "test@example.com",
            PasswordHash = "fixture-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static SessionService Sessions(
        IUserRepository repository,
        IJwtTokenService jwt) =>
        new(
            new InMemoryRefreshTokenStore(),
            new InMemorySessionRevocationStore(),
            repository,
            jwt,
            new RefreshTokenOptions(),
            TimeProvider.System);

    private sealed class LoginRepository : IUserRepository
    {
        public User? User { get; init; }

        public string? RequestedEmail { get; private set; }

        public Task<User?> GetByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedEmail = normalizedEmail;
            return Task.FromResult(User);
        }

        public Task<User?> GetByIdAsync(
            string id,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(User?.Id == id ? User : null);
        }

        public Task<User> CreateAsync(
            User user,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class LoginHasher : IPasswordHasher
    {
        public PasswordVerification Verification { get; init; } =
            new(false);

        public int VerifyCalls { get; private set; }

        public int DummyCalls { get; private set; }

        public string Hash(string password) =>
            throw new NotSupportedException();

        public PasswordVerification Verify(
            string password,
            string encodedHash)
        {
            VerifyCalls++;
            return Verification;
        }

        public void VerifyDummy(string password)
        {
            DummyCalls++;
        }
    }

    private sealed class LoginJwt : IJwtTokenService
    {
        public int CreateCalls { get; private set; }

        public AccessToken CreateAccessToken(User user, string sessionId)
        {
            CreateCalls++;

            return new AccessToken(
                "fixture-token",
                "Bearer",
                900,
                DateTimeOffset.UtcNow.AddMinutes(15),
                "fixture-jti");
        }

        public ClaimsPrincipal ValidateAccessToken(string token) =>
            throw new NotSupportedException();
    }
}
