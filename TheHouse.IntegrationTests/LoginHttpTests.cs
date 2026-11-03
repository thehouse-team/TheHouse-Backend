using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

public sealed class LoginHttpTests
{
    private const string Path = "/api/v1/auth/login";

    [Fact]
    public async Task Valid_credentials_return_200_and_valid_access_token()
    {
        using var factory = new LoginFactory();

        var hasher =
            factory.Services
                .GetRequiredService<IPasswordHasher>();

        const string password =
            "fixture-value-123";

        factory.Store.User = User(
            hasher.Hash(password));

        using var client = factory.CreateHttpsClient();

        using var response =
            await client.PostAsJsonAsync(
                Path,
                new
                {
                    email = " Test@Example.COM ",
                    password
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "no-store",
            response.Headers.CacheControl?.ToString());

        Assert.Contains(
            response.Headers.Pragma,
            header => header.Name == "no-cache");

        using var document =
            JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        Assert.Equal(
            new[]
            {
                "accessToken",
                "tokenType",
                "expiresIn"
            },
            document.RootElement
                .EnumerateObject()
                .Select(property => property.Name));

        Assert.Equal(
            "Bearer",
            document.RootElement
                .GetProperty("tokenType")
                .GetString());

        Assert.Equal(
            900,
            document.RootElement
                .GetProperty("expiresIn")
                .GetInt32());

        var token =
            document.RootElement
                .GetProperty("accessToken")
                .GetString()!;

        var jwt =
            factory.Services
                .GetRequiredService<IJwtTokenService>();

        var principal =
            jwt.ValidateAccessToken(token);

        Assert.Equal(
            "665f1c2a9b1e4a0012345678",
            principal.FindFirst("sub")?.Value);

        var logs = string.Join(
            Environment.NewLine,
            factory.Logs);

        Assert.DoesNotContain(
            password,
            logs,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            token,
            logs,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_user_and_wrong_password_return_same_generic_401()
    {
        using var unknownFactory =
            new LoginFactory();

        using var unknownClient =
            unknownFactory.CreateHttpsClient();

        using var unknown =
            await unknownClient.PostAsJsonAsync(
                Path,
                new
                {
                    email = "test@example.com",
                    password = "fixture-value"
                });

        await AssertProblem(
            unknown,
            401,
            "Invalid credentials");

        using var wrongFactory =
            new LoginFactory();

        var hasher =
            wrongFactory.Services
                .GetRequiredService<IPasswordHasher>();

        wrongFactory.Store.User =
            User(hasher.Hash("correct-value"));

        using var wrongClient =
            wrongFactory.CreateHttpsClient();

        using var wrong =
            await wrongClient.PostAsJsonAsync(
                Path,
                new
                {
                    email = "test@example.com",
                    password = "wrong-value"
                });

        await AssertProblem(
            wrong,
            401,
            "Invalid credentials");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"email\":4,\"password\":\"x\"}")]
    [InlineData("{\"email\":\"invalid\",\"password\":\"x\"}")]
    [InlineData("{\"email\":\"test@example.com\",\"password\":\"\"}")]
    [InlineData("{\"email\":\"test@example.com\",\"password\":\"x\",\"role\":\"admin\"}")]
    public async Task Invalid_input_returns_controlled_400(
        string json)
    {
        using var factory = new LoginFactory();
        using var client = factory.CreateHttpsClient();

        using var response =
            await client.PostAsync(
                Path,
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"));

        await AssertProblem(
            response,
            400,
            "Validation failed");
    }

    [Fact]
    public async Task Unsupported_content_type_returns_415()
    {
        using var factory = new LoginFactory();
        using var client = factory.CreateHttpsClient();

        using var response =
            await client.PostAsync(
                Path,
                new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "text/plain"));

        await AssertProblem(
            response,
            415,
            "Unsupported media type");
    }

    [Fact]
    public async Task Email_limit_rejects_sixth_attempt()
    {
        using var factory = new LoginFactory();
        using var client = factory.CreateHttpsClient();

        for (var i = 0; i < 5; i++)
        {
            using var response =
                await client.PostAsJsonAsync(
                    Path,
                    new
                    {
                        email = "test@example.com",
                        password = "wrong"
                    });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        using var rejected =
            await client.PostAsJsonAsync(
                Path,
                new
                {
                    email = "test@example.com",
                    password = "wrong"
                });

        await AssertProblem(
            rejected,
            429,
            "Too many requests");

        Assert.Equal(
            TimeSpan.FromSeconds(60),
            rejected.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Ip_limit_rejects_twenty_first_attempt_across_distinct_emails()
    {
        using var factory = new LoginFactory();
        using var client = factory.CreateHttpsClient();

        for (var i = 0; i < 20; i++)
        {
            using var response =
                await client.PostAsJsonAsync(
                    Path,
                    new
                    {
                        email = $"user{i}@example.com",
                        password = "wrong"
                    });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        using var rejected =
            await client.PostAsJsonAsync(
                Path,
                new
                {
                    email = "user20@example.com",
                    password = "wrong"
                });

        await AssertProblem(
            rejected,
            429,
            "Too many requests");
    }

    [Fact]
    public async Task Redis_failure_returns_503()
    {
        using var factory = new LoginFactory();
        factory.RateLimit.Unavailable = true;

        using var client = factory.CreateHttpsClient();

        using var response =
            await client.PostAsJsonAsync(
                Path,
                new
                {
                    email = "test@example.com",
                    password = "fixture-value"
                });

        await AssertProblem(
            response,
            503,
            "Service unavailable");
    }

    [Fact]
    public async Task Mongo_failure_returns_503()
    {
        using var factory = new LoginFactory();
        factory.Store.Unavailable = true;

        using var client = factory.CreateHttpsClient();

        using var response =
            await client.PostAsJsonAsync(
                Path,
                new
                {
                    email = "test@example.com",
                    password = "fixture-value"
                });

        await AssertProblem(
            response,
            503,
            "Service unavailable");
    }

    private static User User(string hash) =>
        new()
        {
            Id = "665f1c2a9b1e4a0012345678",
            Email = "test@example.com",
            PasswordHash = hash,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static async Task AssertProblem(
        HttpResponseMessage response,
        int status,
        string title)
    {
        Assert.Equal(
            status,
            (int)response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var text =
            await response.Content.ReadAsStringAsync();

        using var document =
            JsonDocument.Parse(text);

        Assert.Equal(
            status,
            document.RootElement
                .GetProperty("status")
                .GetInt32());

        Assert.Equal(
            title,
            document.RootElement
                .GetProperty("title")
                .GetString());

        Assert.Equal(
            Path,
            document.RootElement
                .GetProperty("instance")
                .GetString());

        Assert.False(
            string.IsNullOrWhiteSpace(
                document.RootElement
                    .GetProperty("traceId")
                    .GetString()));

        Assert.DoesNotContain(
            "stack",
            text,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "password",
            text,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Mongo",
            text,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Redis",
            text,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "C:\\",
            text,
            StringComparison.OrdinalIgnoreCase);
    }
}
