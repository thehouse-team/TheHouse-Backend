using System.Net;
using System.Text.Json;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-07 T09: proyección segura y errores sanitizados sobre HTTP real
// con fakes compartidos (sin Docker/Testcontainers).
public sealed class PropertySecurityTests
{
    private const string ListPath = "/api/v1/properties";

    // Este identificador sensible no debe aparecer nunca: si el DTO
    // se expande sin revisión de spec, la prueba lo detecta.
    private const string SensitiveOwner =
        "owner-0123456789abcdef@example.com";

    [Fact]
    public async Task List_and_detail_never_expose_private_fields()
    {
        using var factory = new PropertyFactory();
        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        var list = await client.GetStringAsync(ListPath);
        var detail = await client.GetStringAsync(
            $"{ListPath}/0123456789abcdef01234561");

        foreach (var body in new[] { list, detail })
        {
            Assert.DoesNotContain("ownerId", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exactAddress", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("address", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("latitude", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("longitude", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("contactEmail", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("moderationStatus", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(SensitiveOwner, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Errors_have_no_stack_trace_or_mongo_details()
    {
        using var factory = new PropertyFactory
        {
            Properties = { Unavailable = true }
        };

        using var factoryHealthy = new PropertyFactory();
        Seed(factoryHealthy.Properties);

        using var client = factory.CreateHttpsClient();
        using var clientHealthy = factoryHealthy.CreateHttpsClient();

        using var unavailable503 = await client.GetAsync(ListPath);
        using var validation400 = await client.GetAsync(
            $"{ListPath}?page=0");
        using var notFound404 = await clientHealthy.GetAsync(
            $"{ListPath}/665f1c2a9b1e4a0012999990");

        foreach (var response in
            new[] { unavailable503, validation400, notFound404 })
        {
            var body = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("at TheHouse", body, StringComparison.Ordinal);
            Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("at ", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Mongo", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("mongodb", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("C:\\", body, StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable503.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, validation400.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notFound404.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_is_shared_between_endpoints()
    {
        using var factory = new PropertyFactory
        {
            RequestsPerWindow = 2
        };

        var shared = factory.RateLimit;
        Seed(factory.Properties);
        using var client = factory.CreateHttpsClient();

        using var one = await client.GetAsync(ListPath);
        using var two = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234561");

        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);

        // Compartido listado + detalle: el tercero es 429 en ambos.
        using var listThird = await client.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, listThird.StatusCode);

        using var detailThird = await client.GetAsync(
            $"{ListPath}/0123456789abcdef01234560");
        Assert.Equal(HttpStatusCode.TooManyRequests, detailThird.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_store_failure_is_fail_closed_503()
    {
        using var factory = new PropertyFactory();

        factory.RateLimit.Unavailable = true;
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(ListPath);

        await ErrorContractTests.AssertProblem(response, 503);
        Assert.DoesNotContain(
            "Redis",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rate_limit_is_shared_across_replicas()
    {
        var shared = new LogoutTestRateLimitStore();

        using var replicaA = new PropertyFactory(
            rateLimit: shared);

        using var replicaB = new PropertyFactory(
            rateLimit: shared);

        replicaA.RequestsPerWindow = 1;
        replicaB.RequestsPerWindow = 1;

        using var clientA = replicaA.CreateHttpsClient();
        using var clientB = replicaB.CreateHttpsClient();

        // Un consumidor listo en la réplica A…
        using var first = await clientA.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // …la réplica B ve el mismo límite compartido: 429.
        using var second = await clientB.GetAsync(ListPath);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Errors_are_problem_details_with_trace_id()
    {
        using var factory = new PropertyFactory();
        Seed(factory.Properties);

        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            $"{ListPath}/665f1c2a9b1e4a0012999990");

        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = body.RootElement;

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("traceId").GetString()));

        Assert.StartsWith(
            "https://httpstatuses.io/404",
            root.GetProperty("type").GetString());
    }

    // --- Soportes --------------------------------------------------

    private static void Seed(
        PropertyTestStore store)
    {
        store.Properties.Add(
            new Property
            {
                Id = "0123456789abcdef01234560",
                Title = "thehouse-0",
                Description = "Descripción pública de la propiedad.",
                Price = 450.0m,
                Currency = "USD",
                City = "San José",
                UniversityId = "665f1c2a9b1e4a0012345679",
                Bedrooms = 2,
                Bathrooms = 1,
                AreaM2 = 65,
                Amenities = ["wifi"],
                CreatedAt = new DateTimeOffset(
                    2026, 11, 2, 12, 0, 0, TimeSpan.Zero)
            });

        store.Properties.Add(
            new Property
            {
                Id = "0123456789abcdef01234561",
                Title = "thehouse-1",
                Description = "Descripción pública de la propiedad.",
                Price = 451.0m,
                Currency = "USD",
                City = "San José",
                UniversityId = "665f1c2a9b1e4a0012345679",
                Bedrooms = 2,
                Bathrooms = 1,
                AreaM2 = 65,
                Amenities = ["wifi"],
                CreatedAt = new DateTimeOffset(
                    2026, 11, 2, 12, 1, 0, TimeSpan.Zero)
            });
    }
}
