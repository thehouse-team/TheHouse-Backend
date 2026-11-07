using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-06 T08: 204, reutilización 401, token ausente/inválido/expirado
// 401, Authorization mal formado y cuerpo 400, Redis falla 503,
// rate limit 429, cabeceras y Problem Details sanitizados.
public sealed class LogoutHttpTests
{
    private const string Path = "/api/v1/auth/logout";

    private const string Password = "fixture-value-123";

    [Fact]
    public async Task First_logout_is_204_and_reuse_is_401()
    {
        using var factory = new LogoutFactory();
        var token = await LoginTokenAsync(factory);

        using var client = factory.CreateHttpsClient();

        using var first = await PostLogout(client, token);

        Assert.Equal(
            HttpStatusCode.NoContent,
            first.StatusCode);

        Assert.Equal(
            "no-store",
            first.Headers.CacheControl?.ToString());

        Assert.Contains(
            first.Headers.Pragma,
            header => header.Name == "no-cache");

        Assert.Equal(
            string.Empty,
            await first.Content.ReadAsStringAsync());

        var jti = ExtractJti(token);
        var ttl = factory.Revocation.Ttl[jti];

        // TTL hasta `exp`, sin exceder la vida restante.
        Assert.InRange(ttl, 1, 900);

        // No se registra el JWT ni su `jti` (T06/T09).
        var logs = string.Join(
            "\n",
            factory.Logs);

        Assert.DoesNotContain(
            token,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            jti,
            logs,
            StringComparison.Ordinal);

        // Reutilización: el token ya está revocado.
        using var second = await PostLogout(client, token);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            second.StatusCode);

        using var problem = Problem(second);
        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("No autorizado", problem.RootElement.GetProperty("title").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(
                problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Missing_authorization_returns_401()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(
            Path,
            EmptyJson());

        await ErrorContractTests.AssertProblem(response, 401);

        using var problem = Problem(response);
        Assert.Equal("No autorizado", problem.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("Token abc")]
    [InlineData("Bearer")]
    [InlineData("Bearer  ")]
    [InlineData("Bearer a b")]
    public async Task Malformed_authorization_returns_400(
        string header)
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Authorization",
            header);

        using var response = await client.PostAsync(
            Path,
            EmptyJson());

        await ErrorContractTests.AssertProblem(response, 400);

        using var problem = Problem(response);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Validación fallida", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Invalid_signature_is_401()
    {
        using var factory = new LogoutFactory();

        // Token firmado con una clave distinta a la configurada.
        var token = CraftToken(
            "ffffffffffffffffffffffffffffffff",
            DateTimeOffset.UtcNow.AddSeconds(600));

        using var client = factory.CreateHttpsClient();

        using var response = await PostLogout(client, token);

        await ErrorContractTests.AssertProblem(response, 401);

        using var problem = Problem(response);
        Assert.Equal("No autorizado", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Garbage_bearer_token_is_401()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "not-a-jwt");

        using var response = await client.PostAsync(
            Path,
            EmptyJson());

        await ErrorContractTests.AssertProblem(response, 401);
    }

    [Fact]
    public async Task Expired_token_is_401()
    {
        using var factory = new LogoutFactory();

        var token = CraftToken(
            LoginFactory.SigningKey,
            DateTimeOffset.UtcNow.AddSeconds(-3600));

        using var client = factory.CreateHttpsClient();

        using var response = await PostLogout(client, token);

        await ErrorContractTests.AssertProblem(response, 401);
    }

    [Fact]
    public async Task Revocation_store_failure_is_503_fail_closed()
    {
        using var factory = new LogoutFactory();

        factory.Revocation.Unavailable = true;

        var token = await LoginTokenAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var response = await PostLogout(client, token);

        await ErrorContractTests.AssertProblem(response, 503);

        using var problem = Problem(response);
        Assert.Equal(503, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Servicio no disponible", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unexpected_body_is_400()
    {
        using var factory = new LogoutFactory();
        var token = await LoginTokenAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path)
        {
            Content = new StringContent(
                "{\"unexpected\":\"body\"}",
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);

        await ErrorContractTests.AssertProblem(response, 400);
    }

    [Fact]
    public async Task Token_via_query_string_is_ignored_401()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(
            Path + "?token=whatever",
            EmptyJson());

        await ErrorContractTests.AssertProblem(response, 401);
    }

    [Fact]
    public async Task Rate_limit_is_429_with_retry_after_60()
    {
        using var factory = new LogoutFactory(
            settings: new Dictionary<string, string?>
            {
                ["LogoutRateLimit:LogoutIpPerWindow"] = "2",
                ["LogoutRateLimit:WindowSeconds"] = "60"
            });

        var token = await LoginTokenAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var first = await PostLogout(client, token);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Reutilización (revocado): cuenta contra el límite IP.
        using var second = await PostLogout(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        using var third = await PostLogout(client, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);

        Assert.Equal(
            60,
            third.Headers.RetryAfter!.Delta!.Value
                .TotalSeconds);

        using var problem = Problem(third);
        Assert.Equal("Demasiadas solicitudes", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Revocation_is_shared_across_replicas()
    {
        var shared = new LogoutTestRevocationStore();

        using var replicaA = new LogoutFactory(revocation: shared);
        using var replicaB = new LogoutFactory(revocation: shared);

        var token =
            await LoginTokenAsync(replicaA, "shared@example.com");

        // Logout en la réplica B con un token emitido por la A.
        using var clientB = replicaB.CreateHttpsClient();
        using var logoutB = await PostLogout(clientB, token);
        Assert.Equal(HttpStatusCode.NoContent, logoutB.StatusCode);

        // Reutilización en la réplica A: rechazada 401.
        using var clientA = replicaA.CreateHttpsClient();
        using var reuseA = await PostLogout(clientA, token);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseA.StatusCode);
    }

    [Fact]
    public async Task Logout_route_only_accepts_post()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(Path);

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            response.StatusCode);
    }

    [Fact]
    public async Task Openapi_contains_logout_and_only_authorized_u2_routes()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        var document =
            await client.GetStringAsync(
                "/openapi/v1.json");

        Assert.Contains(
            "/auth/logout",
            document);

        using var json =
            JsonDocument.Parse(document);

        var paths =
            json.RootElement
                .GetProperty("paths")
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray();

        Assert.All(
            paths,
            path => Assert.True(
                path.StartsWith(
                    "/health/",
                    StringComparison.Ordinal) ||
                path is "/api/v1/auth/register"
                    or "/api/v1/auth/login"
                    or "/api/v1/auth/logout"
                    or "/api/v1/auth/refresh"
                    or "/api/v1/users/me"
                    or "/api/v1/properties"
                    or "/api/v1/properties/{id}"
                    or "/api/v1/universities"
                    or "/api/v1/universities/{id}"
                    or "/api/v1/users/me/favorites"
                    or "/api/v1/users/me/favorites/ids"
                    or "/api/v1/users/me/favorites/{propertyId}"
                    or "/api/v1/users/me/visits"
                    or "/api/v1/visits"
                    or "/api/v1/visits/{id}"
                    or "/api/v1/visits/{id}/cancel"
                    or "/api/v1/users/me/profile"
                    or "/api/v1/users/me/university"
                    or "/api/v1/users/me/preferences"
                    or "/api/v1/universities/search"
                    or "/api/v1/landlord/profile"
                    or "/api/v1/landlord/buildings"
                    or "/api/v1/landlord/buildings/{id}"
                    or "/api/v1/landlord/buildings/{id}/rooms/generate"
                    or "/api/v1/landlord/buildings/{id}/rooms"
                    or "/api/v1/landlord/buildings/{id}/publication"
                    or "/api/v1/landlord/buildings/{id}/publish"
                    or "/api/v1/landlord/rooms/{id}"
                    or "/api/v1/landlord/rooms/{id}/duplicate"
                    or "/api/v1/landlord/rooms/{id}/copy-from"
                    or "/api/v1/landlord/rooms/{id}/pause"
                    or "/api/v1/landlord/rooms/{id}/resume",
                $"Unexpected OpenAPI path: {path}"));
    }
    // --- Soportes -------------------------------------------------

    private static async Task<string> LoginTokenAsync(
        LogoutFactory factory,
        string email = "logout-user@example.com")
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

    private static User User(string passwordHash) =>
        new()
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = "logout-user@example.com",
            PasswordHash = passwordHash,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static async Task<HttpResponseMessage> PostLogout(
        HttpClient client,
        string token)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Path)
        {
            Content = EmptyJson()
        };

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request);
    }

    private static HttpContent EmptyJson() =>
        new StringContent(
            string.Empty,
            Encoding.UTF8,
            "application/json");

    private static string CraftToken(
        string signingKey,
        DateTimeOffset expiration)
    {
        var handler = new JwtSecurityTokenHandler();

        var token = new JwtSecurityToken(
            issuer: "thehouse-api",
            audience: "thehouse-clients",
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    "665f1c2a9b1e4a0012345678"),
                new Claim(
                    JwtRegisteredClaimNames.Email,
                    "logout-user@example.com"),
                new Claim(
                    JwtRegisteredClaimNames.Jti,
                    "0123456789abcdef0123456789abcdef"),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    expiration.AddSeconds(-900)
                        .ToUnixTimeSeconds()
                        .ToString(
                            System.Globalization
                                .CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64)
            ],
            notBefore: expiration.AddSeconds(-900)
                .UtcDateTime,
            expires: expiration.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    private static string ExtractJti(string token) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .First(claim =>
                claim.Type == JwtRegisteredClaimNames.Jti)
            .Value;

    private static JsonDocument Problem(
        HttpResponseMessage response) =>
        JsonDocument.Parse(
            response.Content.ReadAsStringAsync()
                .GetAwaiter()
                .GetResult());
}
