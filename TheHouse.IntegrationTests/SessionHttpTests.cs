using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-10 T01–T03, T05–T08: contrato HTTP de refresh, /users/me y logout
// integrado, con almacenes en memoria que simulan MongoDB y Redis.
public sealed class SessionHttpTests
{
    private const string Password = "Correct-Horse-Battery-9";
    private const string UserId = "665f1c2a9b1e4a0012345678";
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string MePath = "/api/v1/users/me";

    [Fact]
    public async Task Refresh_returns_login_shape_and_rotates_token()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var response = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "accessToken", "tokenType", "expiresIn", "refreshToken" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        var rotated = document.RootElement.GetProperty("refreshToken").GetString();
        Assert.True(SessionTokens.IsWellFormedRefreshToken(rotated));
        Assert.NotEqual(login.RefreshToken, rotated);
        Assert.Equal("Bearer", document.RootElement.GetProperty("tokenType").GetString());
        Assert.Equal(900, document.RootElement.GetProperty("expiresIn").GetInt32());
    }

    [Fact]
    public async Task Reusing_old_refresh_token_revokes_the_whole_session()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var first = await RefreshAsync(client, login.RefreshToken);
        var renewed = await ReadTokensAsync(first);

        using var reuse = await RefreshAsync(client, login.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // La sesión completa quedó revocada: el token nuevo y su access token tampoco sirven.
        using var afterReuse = await RefreshAsync(client, renewed.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
        using var me = await GetMeAsync(client, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Users_me_returns_only_real_fields()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var response = await GetMeAsync(client, login.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "id", "email", "createdAt" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(UserId, document.RootElement.GetProperty("id").GetString());
        Assert.Equal("session-user@example.com", document.RootElement.GetProperty("email").GetString());
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer not-a-jwt")]
    public async Task Users_me_without_valid_token_is_401(string? authorization)
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, MePath);
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_me_for_deleted_user_is_401()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        factory.Store.User = null;
        using var client = factory.CreateHttpsClient();

        using var response = await GetMeAsync(client, login.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_stays_204_and_revokes_refresh_token_and_session()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var refresh = await RefreshAsync(client, login.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var me = await GetMeAsync(client, login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Session_revoked_on_one_replica_is_rejected_on_another()
    {
        var jti = new LogoutTestRevocationStore();
        var refreshTokens = new InMemoryRefreshTokenStore();
        var sessions = new InMemorySessionRevocationStore();
        using var replicaA = new LogoutFactory(jti, refreshTokens: refreshTokens, sessionRevocation: sessions);
        using var replicaB = new LogoutFactory(jti, refreshTokens: refreshTokens, sessionRevocation: sessions);
        var login = await LoginAsync(replicaA);
        replicaB.Store.User = replicaA.Store.User;
        using var clientA = replicaA.CreateHttpsClient();
        using var clientB = replicaB.CreateHttpsClient();

        // Reuso detectado en la réplica A...
        using var first = await RefreshAsync(clientA, login.RefreshToken);
        var renewed = await ReadTokensAsync(first);
        using var reuse = await RefreshAsync(clientA, login.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ...y la réplica B rechaza de inmediato el access token de esa sesión.
        using var me = await GetMeAsync(clientB, renewed.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData("{", 400)]
    [InlineData("{}", 400)]
    [InlineData("{\"refreshToken\":\"x\",\"extra\":1}", 400)]
    [InlineData("{\"refreshToken\":\"too-short\"}", 401)]
    [InlineData("{\"refreshToken\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}", 401)]
    public async Task Refresh_validates_body(string body, int expected)
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(
            RefreshPath,
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_rejects_wrong_content_type_and_large_body()
    {
        using var factory = new LogoutFactory();
        using var client = factory.CreateHttpsClient();

        using var text = await client.PostAsync(RefreshPath, new StringContent("{}", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, text.StatusCode);

        using var large = await client.PostAsync(
            RefreshPath,
            new StringContent("{\"refreshToken\":\"" + new string('a', 2000) + "\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
    }

    [Fact]
    public async Task Refresh_is_rate_limited_per_ip()
    {
        using var factory = new LogoutFactory(settings: new Dictionary<string, string?>
        {
            ["RefreshToken:RefreshIpPerWindow"] = "2",
        });
        using var client = factory.CreateHttpsClient();
        var token = SessionTokens.NewRefreshToken();

        using var one = await RefreshAsync(client, token);
        using var two = await RefreshAsync(client, token);
        using var three = await RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, one.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, two.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, three.StatusCode);
        Assert.Equal("60", three.Headers.RetryAfter?.ToString());
    }

    [Fact]
    public async Task Redis_unavailable_during_refresh_is_503()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        factory.SessionRevocation.Unavailable = true;
        using var client = factory.CreateHttpsClient();

        using var response = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_session_storage_is_503_and_issues_no_tokens()
    {
        using var factory = new LogoutFactory();
        SeedUser(factory);
        factory.RefreshTokens.Unavailable = true;
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "session-user@example.com", password = Password });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Concurrent_refresh_with_same_token_succeeds_at_most_once()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => RefreshAsync(client, login.RefreshToken)));

        Assert.True(responses.Count(response => response.StatusCode == HttpStatusCode.OK) <= 1);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized }));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Refresh_tokens_and_session_ids_never_appear_in_logs()
    {
        using var factory = new LogoutFactory();
        var login = await LoginAsync(factory);
        using var client = factory.CreateHttpsClient();
        using var refreshed = await RefreshAsync(client, login.RefreshToken);
        var renewed = await ReadTokensAsync(refreshed);
        using var reuse = await RefreshAsync(client, login.RefreshToken);
        var sessionId = Assert.Single(factory.SessionRevocation.Revoked.Keys);

        var logs = string.Join("\n", factory.Logs);
        Assert.DoesNotContain(login.RefreshToken, logs);
        Assert.DoesNotContain(renewed.RefreshToken, logs);
        Assert.DoesNotContain(SessionTokens.Hash(login.RefreshToken), logs);
        Assert.DoesNotContain(sessionId, logs);
    }

    private static void SeedUser(LogoutFactory factory) =>
        factory.Store.User = new User
        {
            Id = UserId,
            Email = "session-user@example.com",
            PasswordHash = factory.Services.GetRequiredService<IPasswordHasher>().Hash(Password),
            CreatedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
        };

    private static async Task<Tokens> LoginAsync(LogoutFactory factory)
    {
        SeedUser(factory);
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "session-user@example.com", password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new Tokens(
            document.RootElement.GetProperty("accessToken").GetString()!,
            document.RootElement.GetProperty("refreshToken").GetString()!);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(RefreshPath, new { refreshToken });

    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private sealed record Tokens(string AccessToken, string RefreshToken);
}
