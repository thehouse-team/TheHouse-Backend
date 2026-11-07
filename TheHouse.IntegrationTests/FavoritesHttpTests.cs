using System.Net;
using System.Text.Json;
using TheHouse.Application.Common;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-15 T01–T08: contrato HTTP de favoritos, idempotencia, BOLA,
// rate limit y fallo cerrado.
public sealed class FavoritesHttpTests
{
    private const string Base = "/api/v1/users/me/favorites";

    [Fact]
    public async Task Add_is_idempotent_and_listing_returns_contract_shape()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var first = await alice.PutAsync($"{Base}/{PropertyA}", null);
        using var second = await alice.PutAsync($"{Base}/{PropertyA}", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Contains("no-store", first.Headers.CacheControl?.ToString());
        Assert.Single(factory.Favorites.Items);

        using var list = await alice.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("no-store", list.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "items", "page", "pageSize", "totalItems", "totalPages" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(
            new[] { "propertyId", "title", "price", "currency", "city", "universityId", "bedrooms", "bathrooms", "areaM2", "favoritedAt" },
            item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(PropertyA, item.GetProperty("propertyId").GetString());
        Assert.DoesNotContain("dirección", document.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ids_are_newest_first_and_remove_is_idempotent()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        (await alice.PutAsync($"{Base}/{PropertyA}", null)).Dispose();
        await Task.Delay(20);
        (await alice.PutAsync($"{Base}/{PropertyB}", null)).Dispose();

        using var ids = await alice.GetAsync($"{Base}/ids");
        Assert.Equal(HttpStatusCode.OK, ids.StatusCode);
        using (var document = JsonDocument.Parse(await ids.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { PropertyB, PropertyA },
                document.RootElement.GetProperty("propertyIds").EnumerateArray().Select(value => value.GetString()));
        }

        using var removed = await alice.DeleteAsync($"{Base}/{PropertyA}");
        using var again = await alice.DeleteAsync($"{Base}/{PropertyA}");
        using var never = await alice.DeleteAsync($"{Base}/{PropertyC}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, never.StatusCode);
        Assert.Equal(PropertyB, Assert.Single(factory.Favorites.Items).PropertyId);
    }

    [Fact]
    public async Task Concurrent_double_tap_creates_a_single_favorite()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => alice.PutAsync($"{Base}/{PropertyA}", null)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.Single(factory.Favorites.Items);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData("not-an-object-id", HttpStatusCode.BadRequest)]
    [InlineData("0123456789876543ffffffff", HttpStatusCode.NotFound)]
    public async Task Add_rejects_invalid_or_missing_property(string propertyId, HttpStatusCode expected)
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PutAsync($"{Base}/{propertyId}", null);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.Favorites.Items);
    }

    [Fact]
    public async Task Users_never_see_each_others_favorites()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        (await alice.PutAsync($"{Base}/{PropertyA}", null)).Dispose();

        // Bob intenta borrar el favorito de Alice: solo afecta lo suyo.
        (await bob.DeleteAsync($"{Base}/{PropertyA}")).Dispose();
        using var bobIds = await bob.GetAsync($"{Base}/ids");
        using var document = JsonDocument.Parse(await bobIds.Content.ReadAsStringAsync());

        Assert.Empty(document.RootElement.GetProperty("propertyIds").EnumerateArray());
        Assert.Equal(AliceId, Assert.Single(factory.Favorites.Items).UserId);
    }

    [Fact]
    public async Task Requires_a_valid_bearer_token()
    {
        using var factory = new UserResourceFactory();
        using var anonymous = factory.CreateHttpsClient();

        using var response = await anonymous.GetAsync(Base);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Removed_properties_are_dropped_from_list_and_ids()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        (await alice.PutAsync($"{Base}/{PropertyA}", null)).Dispose();
        (await alice.PutAsync($"{Base}/{PropertyB}", null)).Dispose();

        factory.Properties.Properties.RemoveAll(property => property.Id == PropertyA);

        using var list = await alice.GetAsync(Base);
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(PropertyB, Assert.Single(factory.Favorites.Items).PropertyId);
    }

    [Fact]
    public async Task Pagination_is_validated()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.GetAsync($"{Base}?pageSize=51");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_returns_429_with_retry_after()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { FavoritesPerWindow = 2 });
        using var alice = await factory.ClientForAsync(AliceId);

        (await alice.GetAsync($"{Base}/ids")).Dispose();
        (await alice.GetAsync($"{Base}/ids")).Dispose();
        using var limited = await alice.GetAsync($"{Base}/ids");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", limited.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task Fails_closed_when_redis_or_mongo_are_down()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        factory.Favorites.Unavailable = true;
        using var mongoDown = await alice.PutAsync($"{Base}/{PropertyA}", null);
        factory.Favorites.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        using var redisDown = await alice.GetAsync(Base);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);
    }

    [Fact]
    public async Task Limit_of_200_favorites_returns_409_with_code()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        for (var index = 0; index < 200; index++)
        {
            factory.Favorites.Items.Add(new Domain.Entities.Favorite
            {
                UserId = AliceId,
                PropertyId = $"0123456789876543{index:D8}",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        using var response = await alice.PutAsync($"{Base}/{PropertyA}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("favorites_limit", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Conflicto", document.RootElement.GetProperty("title").GetString());
    }
}
