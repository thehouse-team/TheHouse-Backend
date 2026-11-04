using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.IntegrationTests;

public sealed class RegistrationHttpTests
{
    private const string Path = "/api/v1/auth/register";

    [Fact]
    public async Task Creates_201_with_normalized_email_and_exact_public_response()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "registration-fixture");
        using var response = await client.PostAsJsonAsync(Path, new { email = " Test@Example.COM ", password = "  fixture-value-123  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "id", "email", "createdAt" }, body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("test@example.com", body.RootElement.GetProperty("email").GetString());
        Assert.Matches("^[0-9a-f]{24}$", body.RootElement.GetProperty("id").GetString()!);
        Assert.True(body.RootElement.GetProperty("createdAt").TryGetDateTimeOffset(out _));
        var user = factory.Store.Users["test@example.com"];
        var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.Verify("  fixture-value-123  ", user.PasswordHash).IsValid);
        Assert.False(hasher.Verify("fixture-value-123", user.PasswordHash).IsValid);
        var output = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pbkdf2", output);
        Assert.Equal("registration-fixture", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"email\":4,\"password\":\"fixture-value-123\"}")]
    [InlineData("{\"email\":\"bad\",\"password\":\"fixture-value-123\"}")]
    [InlineData("{\"email\":\"test@example.com\",\"password\":\"short\"}")]
    [InlineData("{\"email\":\"test@example.com\",\"password\":\"            \"}")]
    [InlineData("{\"email\":\"test@example.com\",\"password\":1}")]
    public async Task Invalid_input_returns_controlled_400(string json)
    {
        using var factory = new RegistrationFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsync(Path, new StringContent(json, Encoding.UTF8, "application/json"));
        await AssertProblem(response, 400, "Validation failed");
        Assert.Empty(factory.Store.Users);
    }

    [Fact]
    public async Task Duplicate_email_returns_409_without_mongo_details()
    {
        using var factory = new RegistrationFactory();
        using var client = factory.CreateHttpsClient();
        using var created = await client.PostAsJsonAsync(Path, new { email = "test@example.com", password = "fixture-value-123" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await client.PostAsJsonAsync(Path, new { email = " TEST@EXAMPLE.COM ", password = "different-value-123" });
        await AssertProblem(duplicate, 409, "Email already registered");
        Assert.Single(factory.Store.Users);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/problem+json")]
    [InlineData("application/json; charset=iso-8859-1")]
    public async Task Unsupported_content_type_returns_415(string contentType)
    {
        using var factory = new RegistrationFactory();
        using var client = factory.CreateHttpsClient();
        using var content = new StringContent("{}");
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        using var response = await client.PostAsync(Path, content);
        await AssertProblem(response, 415, "Unsupported media type");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Body_limit_applies_with_and_without_content_length(bool chunked)
    {
        using var factory = new RegistrationFactory();
        using var client = factory.CreateHttpsClient();
        using HttpContent content = chunked
            ? new UnknownLengthContent(new string(' ', 8193))
            : new StringContent(new string(' ', 8193), Encoding.UTF8, "application/json");

        content.Headers.ContentType ??=
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync(Path, content);
        await AssertProblem(response, 413, "Payload too large");
        Assert.Empty(factory.Store.Users);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mongo_or_redis_unavailable_returns_503_and_never_creates_a_user(bool redis)
    {
        using var factory = new RegistrationFactory();
        factory.Quota.Unavailable = redis;
        factory.Store.Unavailable = !redis;
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync(Path, new { email = "test@example.com", password = "fixture-value-123" });
        await AssertProblem(response, 503, "Service unavailable");
        Assert.Empty(factory.Store.Users);
    }

    [Fact]
    public async Task No_configured_real_dependencies_means_fail_closed_not_local_fallback()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync(Path, new { email = "test@example.com", password = "fixture-value-123" });
        await AssertProblem(response, 503, "Service unavailable");
    }

    [Fact]
    public async Task Two_hosts_share_the_quota_contract_and_forged_forwarded_headers_do_not_reset_it()
    {
        var quota = new RegistrationTestQuota();
        using var first = new RegistrationFactory(quota: quota);
        using var second = new RegistrationFactory(quota: quota);
        using var clientA = first.CreateHttpsClient();
        using var clientB = second.CreateHttpsClient();
        for (var i = 0; i < 10; i++)
        {
            using var accepted = await (i % 2 == 0 ? clientA : clientB).PostAsync(Path, new StringContent("{}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, accepted.StatusCode);
        }
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.3");
        using var rejected = await clientB.PostAsync(Path, new StringContent("{}", Encoding.UTF8, "application/json"));
        await AssertProblem(rejected, 429, "Too many requests");
        Assert.Equal(TimeSpan.FromSeconds(60), rejected.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Unknown_errors_use_safe_500()
    {
        using var factory = new RegistrationFactory();
        factory.Store.UnexpectedFailure = true;
        using var client = factory.CreateHttpsClient();
        using var response = await client.PostAsJsonAsync(Path, new { email = "test@example.com", password = "fixture-value-123" });
        await AssertProblem(response, 500, "Internal server error");
        Assert.DoesNotContain("internal-collection-marker", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("209999")]
    [InlineData("2000001")]
    [InlineData("invalid-value-marker")]
    public void Invalid_hashing_configuration_fails_at_start_without_echo(string value)
    {
        using var factory = new RegistrationFactory(settings: new Dictionary<string, string?> { ["PasswordHashing:Iterations"] = value });
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateHttpsClient());
        Assert.DoesNotContain(value, error.Message);
    }

    [Fact]
    public void Hashing_work_factor_is_bound_to_a_typed_option()
    {
        using var factory = new RegistrationFactory(settings: new Dictionary<string, string?> { ["PasswordHashing:Iterations"] = "210001" });
        using var client = factory.CreateHttpsClient();
        Assert.Equal(210001, factory.Services.GetRequiredService<IOptions<PasswordHashingOptions>>().Value.Iterations);
    }

    internal static async Task AssertProblem(HttpResponseMessage response, int status, string title)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(text);
        Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
        Assert.StartsWith("https://", body.RootElement.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("detail").GetString()));
        Assert.Equal(Path, body.RootElement.GetProperty("instance").GetString());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), body.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("stack", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mongo", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        public UnknownLengthContent() : this("") { }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await stream.WriteAsync(Encoding.UTF8.GetBytes(body));
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(body)));
    }
}
