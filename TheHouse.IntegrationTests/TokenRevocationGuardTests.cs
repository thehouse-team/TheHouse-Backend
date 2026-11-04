using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TheHouse.Application.Common;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

// NV-06: guardia central de revocación probado como componente real
// contra(HttpContext simulado autenticado) y contra instancias
// "réplica" con ITokenRevocationStore compartido. No cambia el
// contrato de Registration/Login/Logout (el guard solo actúa en
// endpoints con RequireTokenNotRevoked).
public sealed class TokenRevocationGuardTests
{
    private const string Password = "fixture-value-123";

    [Fact]
    public async Task Valid_not_revoked_token_continues()
    {
        using var factory = new LogoutFactory();

        var token = await LoginTokenAsync(factory);
        var service = Guard(factory);

        var (nextRan, status, _, _) =
            await RunGuardAsync(service, token);

        Assert.True(nextRan);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Token_revoked_in_replica_B_is_rejected_in_A()
    {
        var shared = new LogoutTestRevocationStore();

        using var replicaA =
            new LogoutFactory(revocation: shared);
        using var replicaB =
            new LogoutFactory(revocation: shared);

        var token =
            await LoginTokenAsync(replicaA, "guard@example.com");

        // La réplica B emite la revocación del token de la A.
        var serviceB = Guard(replicaB);
        await serviceB.RevokeAsync(token, default);

        var serviceA = Guard(replicaA);

        var (nextRan, status, _, _) =
            await RunGuardAsync(serviceA, token);

        Assert.False(nextRan);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Store_unavailable_is_fail_closed_503()
    {
        using var factory = new LogoutFactory();

        factory.Revocation.Unavailable = true;

        var token = await LoginTokenAsync(factory);
        var service = Guard(factory);

        var (nextRan, status, _, _) =
            await RunGuardAsync(service, token);

        Assert.False(nextRan);
        Assert.Equal(503, status);
    }

    [Fact]
    public async Task Token_without_jti_is_rejected_401()
    {
        using var factory = new LogoutFactory();

        var security = factory.Services
            .GetRequiredService<IJwtTokenService>();

        // Token válido en clave/issuer/audience/claims pero sin `jti`.
        var handler = new JwtSecurityTokenHandler();

        var now = DateTimeOffset.UtcNow;
        var token = handler.WriteToken(new JwtSecurityToken(
            issuer: "thehouse-api",
            audience: "thehouse-clients",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "user-1"),
                new Claim(JwtRegisteredClaimNames.Email, "guard@example.com"),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    now.ToUnixTimeSeconds()
                        .ToString(
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64)
            ],
            notBefore: now.AddSeconds(-1).UtcDateTime,
            expires: now.AddSeconds(300).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        LogoutFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256)));

        var (nextRan, status, _, _) =
            await RunGuardAsync(Guard(factory), token);

        Assert.Equal(401, status);
        Assert.False(nextRan);
    }

    [Fact]
    public async Task Guard_never_logs_token_or_authorization()
    {
        using var factory = new LogoutFactory();
        var logger = new CapturingLogger();
        var token = await LoginTokenAsync(factory);
        var service = Guard(factory, logger);

        await RunGuardAsync(service, token);
        await service.RevokeAsync(token, default);
        await RunGuardAsync(service, token);

        var logs = string.Join(
            "\n",
            logger.Messages);

        var jti = ExtractJti(token);

        Assert.DoesNotContain(
            token,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            jti,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Bearer " + token,
            logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Endpoints_without_guard_pass_through()
    {
        using var factory = new LogoutFactory();
        var service = Guard(factory);

        // Contexto sin endpoint marcado con RevocationGuard.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var nextRan = false;
        var middleware = new TokenRevocationMiddleware(
            _ =>
            {
                nextRan = true;
                return Task.CompletedTask;
            },
            service,
            new CapturingLogger());

        await middleware.InvokeAsync(context);

        Assert.True(nextRan);
    }

    // --- Soportes -------------------------------------------------

    private static LogoutService Guard(
        LogoutFactory factory,
        CapturingLogger? logger = null) =>
        new(
            factory.Services
                .GetRequiredService<IJwtTokenService>(),
            factory.Revocation,
            new FixedTimeProvider());

    private static async Task<string> LoginTokenAsync(
        LogoutFactory factory,
        string email = "guard-user@example.com")
    {
        factory.Store.User = new User
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = email,
            PasswordHash = factory.Services
                .GetRequiredService<IPasswordHasher>()
                .Hash(Password),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("accessToken")
            .GetString()!;
    }

    private static async Task<(
        bool NextRan,
        int Status,
        string Body,
        CapturingLogger Logger)> RunGuardAsync(
        LogoutService service,
        string token)
    {
        return await RunGuardAsync(
            service,
            token,
            new CapturingLogger());
    }

    private static async Task<(
        bool NextRan,
        int Status,
        string Body,
        CapturingLogger Logger)> RunGuardAsync(
        LogoutService service,
        string token,
        CapturingLogger logger)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(
            new Endpoint(
                null,
                new EndpointMetadataCollection(
                    RevocationGuard.Instance),
                "test"));

        context.Request.Headers.Authorization =
            "Bearer " + token;

        var nextRan = false;

        var middleware = new TokenRevocationMiddleware(
            _ =>
            {
                nextRan = true;
                return Task.CompletedTask;
            },
            service,
            logger);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(
            context.Response.Body);

        var body = await reader.ReadToEndAsync();

        return (nextRan, context.Response.StatusCode, body, logger);
    }

    private static string ExtractJti(string token) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .First(claim =>
                claim.Type == JwtRegisteredClaimNames.Jti)
            .Value;

    private sealed class FixedTimeProvider : TimeProvider
    {
    }

    private sealed class CapturingLogger : ILogger<TokenRevocationMiddleware>
    {
        public readonly List<string> Messages = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(
                formatter(state, exception) +
                (exception is null
                    ? ""
                    : exception.ToString()));
        }
    }
}
