using System.Net;
using System.Text.Json;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-08 T10: proyección segura, Problem Details exactos y rate limit
// compartido (listado + detalle, y entre réplicas con el mismo store).
public sealed class UniversitySecurityTests
{
    private const string ListPath = "/api/v1/universities";

    private static readonly string[] Forbidden =
    [
        "ownerId", "exactAddress", "address", "latitude",
        "longitude", "contactEmail", "email", "phone",
        "telephone", "moderationStatus", "connectionString"
    ];

    [Fact]
    public async Task List_and_detail_never_expose_private_fields()
    {
        using var factory = new UniversityFactory();
        var ids = Seed(factory.Universities);
        using var client = factory.CreateHttpsClient();

        var list = await client.GetStringAsync(ListPath);
        var detail = await client.GetStringAsync(
            $"{ListPath}/{ids["UTTT"]}");

        foreach (var body in new[] { list, detail })
        {
            foreach (var forbidden in Forbidden)
            {
                Assert.DoesNotContain(
                    forbidden,
                    body,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData(400, "Validación fallida", "?page=0")]
    [InlineData(404, "Recurso no encontrado", "/665f1c2a9b1e4a0012999990")]
    [InlineData(503, "Servicio no disponible", "")]
    public async Task Problem_details_use_exact_contracts(
        int status,
        string title,
        string suffix)
    {
        using var factory = new UniversityFactory
        {
            Universities = { Unavailable = suffix.Length == 0 }
        };

        using var client = factory.CreateHttpsClient();

        using var response =
            await client.GetAsync(ListPath + suffix);

        await ErrorContractTests.AssertProblem(response, status);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.Equal(title, root.GetProperty("title").GetString());

        var raw =
            await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Mongo", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Redis", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at TheHouse", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rate_limit_is_429_with_exact_title_and_retry()
    {
        using var factory = new UniversityFactory
        {
            RequestsPerWindow = 1
        };

        using var client = factory.CreateHttpsClient();

        using var first = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var exceeded = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
        Assert.Equal(60, exceeded.Headers.RetryAfter!.Delta!.Value.TotalSeconds);

        await ErrorContractTests.AssertProblem(exceeded, 429);

        using var body = JsonDocument.Parse(
            await exceeded.Content.ReadAsStringAsync());

        Assert.Equal(
            "Demasiadas solicitudes",
            body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Rate_limit_store_failure_is_fail_closed_503()
    {
        using var factory = new UniversityFactory();

        factory.RateLimit.Unavailable = true;

        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        await ErrorContractTests.AssertProblem(response, 503);

        var raw =
            await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Redis", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rate_limit_is_shared_between_list_and_detail()
    {
        using var factory = new UniversityFactory
        {
            RequestsPerWindow = 2
        };

        var ids = Seed(factory.Universities);

        using var client = factory.CreateHttpsClient();

        using var one = await client.GetAsync(ListPath);
        using var two = await client.GetAsync(
            $"{ListPath}/{ids["UTTT"]}");

        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);

        using var thirdList = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, thirdList.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_is_shared_across_replicas()
    {
        var shared = new LogoutTestRateLimitStore();

        using var replicaA = new UniversityFactory(rateLimit: shared);
        using var replicaB = new UniversityFactory(rateLimit: shared);

        replicaA.RequestsPerWindow = 1;
        replicaB.RequestsPerWindow = 1;

        using var clientA = replicaA.CreateHttpsClient();
        using var clientB = replicaB.CreateHttpsClient();

        using var first = await clientA.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // La réplica B comparte el contador: 429 sin fallback local.
        using var second = await clientB.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    // --- Soportes --------------------------------------------------

    internal static Dictionary<string, string> Seed(
        UniversityTestStore store) =>
        UniversityHttpTests.Seed(store);
}
