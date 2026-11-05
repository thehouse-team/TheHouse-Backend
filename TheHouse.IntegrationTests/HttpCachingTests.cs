using System.Net;
using System.Net.Http.Headers;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-07 T12 y NV-08 T13: caché HTTP condicional (ETag débil, 304,
// Cache-Control) en las lecturas públicas de propiedades y universidades.
public sealed class HttpCachingTests
{
    private const string PropertiesPath = "/api/v1/properties";
    private const string PropertyId = "0123456789abcdef0123ca01";
    private const string UniversitiesPath = "/api/v1/universities";

    public static TheoryData<string> CacheablePaths => new()
    {
        PropertiesPath,
        $"{PropertiesPath}/{PropertyId}",
        UniversitiesPath,
        // UTTT en UniversityHttpTests.Seed: id derivado de la longitud del código.
        $"{UniversitiesPath}/012345678987654321000004"
    };

    [Theory]
    [MemberData(nameof(CacheablePaths))]
    public async Task Success_has_weak_etag_and_public_cache_control(string path)
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(path);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);
        Assert.True(etag.IsWeak);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl?.MaxAge);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [MemberData(nameof(CacheablePaths))]
    public async Task Same_content_keeps_the_same_etag(string path)
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(path);

        using var first = await client.GetAsync(path);
        using var second = await client.GetAsync(path);

        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
    }

    [Theory]
    [MemberData(nameof(CacheablePaths))]
    public async Task Matching_if_none_match_returns_304_without_body(string path)
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(path);
        using var first = await client.GetAsync(path);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(first.Headers.ETag, response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.Public);
    }

    [Theory]
    [InlineData("\"otro\", {0}")]
    [InlineData("*")]
    public async Task List_or_wildcard_if_none_match_returns_304(string headerTemplate)
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(UniversitiesPath);
        using var first = await client.GetAsync(UniversitiesPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, UniversitiesPath);
        request.Headers.TryAddWithoutValidation(
            "If-None-Match",
            string.Format(headerTemplate, first.Headers.ETag!.ToString()));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Strong_form_of_the_same_tag_matches_with_weak_comparison()
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(PropertiesPath);
        using var first = await client.GetAsync(PropertiesPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, PropertiesPath);
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(first.Headers.ETag!.Tag));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Changed_content_returns_200_with_a_new_etag()
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(PropertiesPath);
        using var first = await client.GetAsync(PropertiesPath);

        factories.Properties.Properties.Properties.Add(NewProperty("0123456789abcdef0123ca02", "Nueva"));

        using var request = new HttpRequestMessage(HttpMethod.Get, PropertiesPath);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(first.Headers.ETag, response.Headers.ETag);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Non_matching_if_none_match_returns_full_body()
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(UniversitiesPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, UniversitiesPath);
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"no-coincide\"", isWeak: true));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData($"{PropertiesPath}/0123456789abcdef0123ffff", HttpStatusCode.NotFound)]
    [InlineData($"{PropertiesPath}?pageSize=1000", HttpStatusCode.BadRequest)]
    [InlineData($"{UniversitiesPath}/0123456789abcdef0123ffff", HttpStatusCode.NotFound)]
    [InlineData($"{UniversitiesPath}?page=abc", HttpStatusCode.BadRequest)]
    public async Task Errors_are_not_cacheable(string path, HttpStatusCode expected)
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(path);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        using var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Not_modified_still_counts_against_the_rate_limit()
    {
        using var factories = new Factories(requestsPerWindow: 2);
        using var client = factories.ClientFor(PropertiesPath);
        using var first = await client.GetAsync(PropertiesPath);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, PropertiesPath);
        conditional.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var notModified = await client.SendAsync(conditional);

        using var limited = new HttpRequestMessage(HttpMethod.Get, PropertiesPath);
        limited.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var response = await client.SendAsync(limited);

        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task Etag_does_not_depend_on_accept_encoding()
    {
        using var factories = new Factories();
        using var client = factories.ClientFor(UniversitiesPath);
        using var plain = await client.GetAsync(UniversitiesPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, UniversitiesPath);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        using var encoded = await client.SendAsync(request);

        Assert.Equal(plain.Headers.ETag, encoded.Headers.ETag);
    }

    private static Property NewProperty(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Description = "Descripción pública de la propiedad.",
        Price = 3500m,
        Currency = "MXN",
        City = "Tula de Allende",
        UniversityId = "665f1c2a9b1e4a0012345679",
        Bedrooms = 1,
        Bathrooms = 1,
        AreaM2 = 20,
        Amenities = ["wifi"],
        CreatedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)
    };

    private sealed class Factories : IDisposable
    {
        private readonly int requestsPerWindow;
        private PropertyFactory? properties;
        private UniversityFactory? universities;

        public Factories(int requestsPerWindow = 60) => this.requestsPerWindow = requestsPerWindow;

        public PropertyFactory Properties => properties ??= CreateProperties();

        public HttpClient ClientFor(string path) =>
            path.StartsWith(UniversitiesPath, StringComparison.Ordinal)
                ? (universities ??= CreateUniversities()).CreateHttpsClient()
                : Properties.CreateHttpsClient();

        private PropertyFactory CreateProperties()
        {
            var factory = new PropertyFactory { RequestsPerWindow = requestsPerWindow };
            factory.Properties.Properties.Add(NewProperty(PropertyId, "Cuarto cerca de la UTTT"));
            return factory;
        }

        private UniversityFactory CreateUniversities()
        {
            var factory = new UniversityFactory { RequestsPerWindow = requestsPerWindow };
            UniversityHttpTests.Seed(factory.Universities);
            return factory;
        }

        public void Dispose()
        {
            properties?.Dispose();
            universities?.Dispose();
        }
    }
}
