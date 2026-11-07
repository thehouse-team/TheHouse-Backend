using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.RoomTestFixtures;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-15 1.2.0 T01–T08: contrato HTTP de favoritos sobre cuartos publicados,
// transición desde `propertyId`, idempotencia, BOLA, rate limit y fallo
// cerrado.
public sealed class FavoritesHttpTests
{
    private const string Base = "/api/v1/users/me/favorites";

    [Fact]
    public async Task Add_is_idempotent_and_listing_returns_contract_shape()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];

        using var first = await alice.PutAsync($"{Base}/{room}", null);
        using var second = await alice.PutAsync($"{Base}/{room}", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Contains("no-store", first.Headers.CacheControl?.ToString());
        Assert.Equal(room, Assert.Single(factory.Favorites.Items).RoomId);

        using var list = await alice.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("no-store", list.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "items", "page", "pageSize", "totalItems", "totalPages" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(
            new[] { "roomId", "buildingId", "title", "price", "currency", "roomType", "bathroom", "city", "approxLocation", "demo", "favoritedAt", "propertyId" },
            item.EnumerateObject().Select(property => property.Name));
        Assert.Equal(room, item.GetProperty("roomId").GetString());
        Assert.Equal(LegacyOf(factory, room), item.GetProperty("propertyId").GetString());
        Assert.True(item.GetProperty("demo").GetBoolean());

        // Sin dirección ni coordenadas exactas ni dueño.
        var building = factory.Buildings.Buildings[factory.Rooms.Rooms[room].BuildingId];
        var raw = document.RootElement.GetRawText();
        Assert.DoesNotContain(building.Address.Street, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(building.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture), raw, StringComparison.Ordinal);
        Assert.DoesNotContain(DemoRoomSeedService.SystemLandlordId, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ids_are_newest_first_and_remove_is_idempotent()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var rooms = RoomIds(factory);

        (await alice.PutAsync($"{Base}/{rooms[0]}", null)).Dispose();
        await Task.Delay(20);
        (await alice.PutAsync($"{Base}/{rooms[1]}", null)).Dispose();

        using var ids = await alice.GetAsync($"{Base}/ids");
        Assert.Equal(HttpStatusCode.OK, ids.StatusCode);
        using (var document = JsonDocument.Parse(await ids.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { "roomIds", "propertyIds" },
                document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(
                new[] { rooms[1], rooms[0] },
                document.RootElement.GetProperty("roomIds").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(
                new[] { LegacyOf(factory, rooms[1]), LegacyOf(factory, rooms[0]) },
                document.RootElement.GetProperty("propertyIds").EnumerateArray().Select(value => value.GetString()));
        }

        using var removed = await alice.DeleteAsync($"{Base}/{rooms[0]}");
        using var again = await alice.DeleteAsync($"{Base}/{rooms[0]}");
        using var never = await alice.DeleteAsync($"{Base}/{rooms[2]}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, never.StatusCode);
        Assert.Equal(rooms[1], Assert.Single(factory.Favorites.Items).RoomId);
    }

    [Fact]
    public async Task Legacy_property_ids_are_accepted_during_the_transition()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];
        var legacy = LegacyOf(factory, room);

        using var byProperty = await alice.PutAsync($"{Base}/{legacy}", null);
        using var byRoom = await alice.PutAsync($"{Base}/{room}", null);

        Assert.Equal(HttpStatusCode.NoContent, byProperty.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byRoom.StatusCode);
        Assert.Equal(room, Assert.Single(factory.Favorites.Items).RoomId);

        using var removed = await alice.DeleteAsync($"{Base}/{legacy}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(factory.Favorites.Items);
    }

    [Fact]
    public async Task Unmigrated_favorites_are_listed_by_room_and_migrated_by_the_command()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var rooms = RoomIds(factory);
        var legacy = LegacyOf(factory, rooms[3]);
        factory.Favorites.Items.Add(new Favorite { UserId = AliceId, LegacyPropertyId = legacy, CreatedAt = DateTimeOffset.UtcNow });
        factory.Favorites.Items.Add(new Favorite { UserId = AliceId, LegacyPropertyId = "0000000000000000000000ff", CreatedAt = DateTimeOffset.UtcNow });

        using var ids = await alice.GetAsync($"{Base}/ids");
        using (var document = JsonDocument.Parse(await ids.Content.ReadAsStringAsync()))
        {
            Assert.Equal(new[] { rooms[3] }, document.RootElement.GetProperty("roomIds").EnumerateArray().Select(value => value.GetString()));
        }

        var (favorites, _) = await factory.Services.GetRequiredService<RoomMigrationService>().MigrateAsync(CancellationToken.None);

        Assert.Equal(new MigrationCounts(1, 0, 1), favorites);
        Assert.Contains(factory.Favorites.Items, item => item.RoomId == rooms[3] && item.LegacyPropertyId is null);
    }

    [Fact]
    public async Task Concurrent_double_tap_creates_a_single_favorite()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => alice.PutAsync($"{Base}/{room}", null)));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.Single(factory.Favorites.Items);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData("not-an-object-id", HttpStatusCode.BadRequest)]
    [InlineData(MissingId, HttpStatusCode.NotFound)]
    public async Task Add_rejects_invalid_or_missing_room(string roomId, HttpStatusCode expected)
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PutAsync($"{Base}/{roomId}", null);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.Favorites.Items);
    }

    [Fact]
    public async Task Unpublished_rooms_cannot_be_added_and_are_hidden_from_list_and_ids()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var rooms = RoomIds(factory);
        (await alice.PutAsync($"{Base}/{rooms[0]}", null)).Dispose();
        (await alice.PutAsync($"{Base}/{rooms[1]}", null)).Dispose();

        var paused = factory.Rooms.Rooms[rooms[0]];
        factory.Rooms.Rooms[paused.Id] = paused with { Status = RoomStatus.Paused };
        var building = factory.Buildings.Buildings[factory.Rooms.Rooms[rooms[2]].BuildingId];
        factory.Buildings.Buildings[building.Id] = building with { Status = BuildingStatus.Suspended };

        using var add = await alice.PutAsync($"{Base}/{rooms[2]}", null);
        using var list = await alice.GetAsync(Base);
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(rooms[1], Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray()).GetProperty("roomId").GetString());
        Assert.Equal(2, factory.Favorites.Items.Count);
    }

    [Fact]
    public async Task Users_never_see_each_others_favorites()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var room = RoomIds(factory)[0];

        (await alice.PutAsync($"{Base}/{room}", null)).Dispose();

        // Bob intenta borrar el favorito de Alice: solo afecta lo suyo.
        (await bob.DeleteAsync($"{Base}/{room}")).Dispose();
        using var bobIds = await bob.GetAsync($"{Base}/ids");
        using var document = JsonDocument.Parse(await bobIds.Content.ReadAsStringAsync());

        Assert.Empty(document.RootElement.GetProperty("roomIds").EnumerateArray());
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
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];

        factory.Favorites.Unavailable = true;
        using var mongoDown = await alice.PutAsync($"{Base}/{room}", null);
        factory.Favorites.Unavailable = false;
        factory.Rooms.Unavailable = true;
        using var roomsDown = await alice.PutAsync($"{Base}/{room}", null);
        factory.Rooms.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        using var redisDown = await alice.GetAsync(Base);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, roomsDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);
    }

    [Fact]
    public async Task Limit_of_200_favorites_returns_409_with_code()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        for (var index = 0; index < 200; index++)
        {
            factory.Favorites.Items.Add(new Favorite
            {
                UserId = AliceId,
                RoomId = $"0123456789876543{index:D8}",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        using var response = await alice.PutAsync($"{Base}/{RoomIds(factory)[0]}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("favorites_limit", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Conflicto", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Legacy_property_catalog_announces_its_deprecation()
    {
        using var factory = new UserResourceFactory();
        using var client = factory.CreateHttpsClient();

        using var list = await client.GetAsync("/api/v1/properties");
        using var detail = await client.GetAsync($"/api/v1/properties/{PropertyA}");

        foreach (var response in new[] { list, detail })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("true", response.Headers.GetValues("Deprecation").Single());
            Assert.Equal("</api/v1/rooms/search>; rel=\"successor-version\"", response.Headers.GetValues("Link").Single());
        }
    }
}
