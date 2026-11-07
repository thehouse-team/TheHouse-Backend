using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-14 T01–T11: búsqueda, mapa, conteo, detalle con sesión y colonias
// sobre el seed de demostración (12 cuartos verificados) y el de colonias.
public sealed class SearchHttpTests
{
    private const string Rooms = "/api/v1/rooms";

    [Fact]
    public async Task Search_returns_published_rooms_with_contract_shape_and_no_private_data()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{Rooms}/search?universityId={Uttt(factory)}&maxDistanceKm=30&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        Assert.Equal(
            new[] { "id", "buildingId", "title", "price", "currency", "roomType", "bathroom", "buildingType", "allowedTenants", "services", "distanceKm", "walkingMinutes", "approxLocation", "thumbnailUrl", "demo" },
            items[0].EnumerateObject().Select(property => property.Name));
        Assert.All(items, item => Assert.True(item.GetProperty("demo").GetBoolean()));
        var distances = items.Select(item => item.GetProperty("distanceKm").GetDouble()).ToArray();
        Assert.Equal(distances.OrderBy(distance => distance), distances);
        foreach (var forbidden in new[] { "ownerId", "address", "system-demo", "Ubicación de ejemplo", "+52" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Approximate_location_hides_the_real_point_and_distances_use_the_cell()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var university = factory.Universities.Universities.Single(item => item.Code == "UTTT");

        using var response = await client.GetAsync($"{Rooms}/search?universityId={university.Id}&maxDistanceKm=30&pageSize=50");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
        {
            var building = factory.Buildings.Buildings[item.GetProperty("buildingId").GetString()!];
            var lat = item.GetProperty("approxLocation").GetProperty("lat").GetDouble();
            var lng = item.GetProperty("approxLocation").GetProperty("lng").GetDouble();
            Assert.False(lat == building.Latitude && lng == building.Longitude);
            Assert.True(ListingRules.DistanceKm(lat, lng, building.Latitude, building.Longitude) < 0.25);
            var fromCell = Math.Round(ListingRules.DistanceKm(lat, lng, university.Latitude!.Value, university.Longitude!.Value), 1);
            Assert.Equal(fromCell, item.GetProperty("distanceKm").GetDouble());
        }
    }

    [Fact]
    public async Task Filters_sort_and_pagination_apply_and_count_matches()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var uttt = Uttt(factory);

        using var cheap = await client.GetAsync($"{Rooms}/search?universityId={uttt}&maxDistanceKm=30&maxPrice=3000&sort=price&pageSize=50");
        using var count = await client.GetAsync($"{Rooms}/count?universityId={uttt}&maxDistanceKm=30&maxPrice=3000");
        using var wifiKitchen = await client.GetAsync($"{Rooms}/search?universityId={uttt}&maxDistanceKm=30&services=wifi,kitchen&pageSize=50");
        using var paged = await client.GetAsync($"{Rooms}/search?universityId={uttt}&maxDistanceKm=30&pageSize=2&page=1");

        using (var document = JsonDocument.Parse(await cheap.Content.ReadAsStringAsync()))
        using (var counted = JsonDocument.Parse(await count.Content.ReadAsStringAsync()))
        {
            var prices = document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("price").GetInt32()).ToArray();
            Assert.NotEmpty(prices);
            Assert.All(prices, price => Assert.True(price <= 3000));
            Assert.Equal(prices.OrderBy(price => price), prices);
            Assert.Equal(document.RootElement.GetProperty("totalItems").GetInt64(), counted.RootElement.GetProperty("count").GetInt32());
        }

        using (var document = JsonDocument.Parse(await wifiKitchen.Content.ReadAsStringAsync()))
        {
            Assert.All(document.RootElement.GetProperty("items").EnumerateArray(), item =>
            {
                var services = item.GetProperty("services").EnumerateArray().Select(value => value.GetString()).ToArray();
                Assert.Contains("wifi", services);
                Assert.Contains("kitchen", services);
            });
        }

        using (var document = JsonDocument.Parse(await paged.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
            Assert.True(document.RootElement.GetProperty("totalPages").GetInt32() >= 2);
        }
    }

    [Theory]
    [InlineData("universityId=nope")]
    [InlineData("universityId={uttt}&maxPrice=100")]
    [InlineData("universityId={uttt}&minPrice=5000&maxPrice=3000")]
    [InlineData("universityId={uttt}&roomType=suite")]
    [InlineData("universityId={uttt}&services=pool")]
    [InlineData("universityId={uttt}&maxDistanceKm=50")]
    [InlineData("universityId={uttt}&sort=random")]
    [InlineData("universityId={uttt}&pageSize=51")]
    [InlineData("")]
    public async Task Invalid_filters_return_400(string query)
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{Rooms}/search?{query.Replace("{uttt}", Uttt(factory))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Unknown_university_returns_404()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync($"{Rooms}/search?universityId=ffffffffffffffffffffffff");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_available_rooms_of_verified_buildings_are_public()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var uttt = Uttt(factory);
        var total = await TotalAsync(client, uttt);

        var room = factory.Rooms.Rooms.Values.First();
        factory.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };
        var otherBuilding = factory.Buildings.Buildings.Values.First(building => building.Id != room.BuildingId);
        factory.Buildings.Buildings[otherBuilding.Id] = otherBuilding with { Status = BuildingStatus.Suspended };

        Assert.Equal(total - 2, await TotalAsync(client, uttt));
    }

    [Fact]
    public async Task Map_returns_markers_inside_a_valid_bbox()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var uttt = Uttt(factory);

        using var map = await client.GetAsync($"{Rooms}/map?universityId={uttt}&maxDistanceKm=30&bbox=-99.45,19.90,-99.20,20.15");
        using var tooBig = await client.GetAsync($"{Rooms}/map?universityId={uttt}&bbox=-99.80,19.60,-99.00,20.40");
        using var missing = await client.GetAsync($"{Rooms}/map?universityId={uttt}");

        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        using (var document = JsonDocument.Parse(await map.Content.ReadAsStringAsync()))
        {
            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.NotEmpty(items);
            Assert.True(items.Length <= SearchService.MaximumMarkers);
            Assert.Equal(new[] { "id", "price", "approxLocation", "distanceKm" }, items[0].EnumerateObject().Select(property => property.Name));
            Assert.All(items, item =>
            {
                var lat = item.GetProperty("approxLocation").GetProperty("lat").GetDouble();
                var lng = item.GetProperty("approxLocation").GetProperty("lng").GetDouble();
                Assert.InRange(lat, 19.90, 20.15);
                Assert.InRange(lng, -99.45, -99.20);
            });
        }

        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Room_detail_requires_session_and_hides_private_data()
    {
        using var factory = await SeededAsync();
        using var anonymous = factory.CreateHttpsClient();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = factory.Rooms.Rooms.Values.First();

        using var guest = await anonymous.GetAsync($"{Rooms}/{room.Id}");
        using var detail = await alice.GetAsync($"{Rooms}/{room.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("no-store", detail.Headers.CacheControl?.ToString());
        var body = await detail.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            new[] { "id", "buildingId", "title", "price", "currency", "roomType", "bathroom", "sizeM2", "furniture", "availableFrom", "minContract", "distanceKm", "walkingMinutes", "approxLocation", "nearestUniversity", "building", "thumbnailUrl", "demo" },
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "id", "name", "type", "allowedTenants", "services", "rules" }, document.RootElement.GetProperty("building").EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("Ubicación de ejemplo", body, StringComparison.Ordinal);
        Assert.DoesNotContain(DemoRoomSeedService.SystemLandlordId, body, StringComparison.Ordinal);

        factory.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Ready };
        using var unpublished = await alice.GetAsync($"{Rooms}/{room.Id}");
        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
    }

    [Fact]
    public async Task Places_are_found_without_accents_and_limited_to_ten()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var uttt = Uttt(factory);

        using var centro = await client.GetAsync($"/api/v1/places/search?q=centro&universityId={uttt}");
        using var accent = await client.GetAsync($"/api/v1/places/search?q=cuauhtemoc&universityId={uttt}");
        using var broad = await client.GetAsync($"/api/v1/places/search?q=an&universityId={uttt}");
        using var none = await client.GetAsync($"/api/v1/places/search?q=zzzz&universityId={uttt}");
        using var invalid = await client.GetAsync($"/api/v1/places/search?q=a&universityId={uttt}");

        using (var document = JsonDocument.Parse(await centro.Content.ReadAsStringAsync()))
        {
            var first = document.RootElement.GetProperty("items")[0];
            Assert.Equal("Centro", first.GetProperty("name").GetString());
            Assert.Equal(new[] { "id", "name", "type", "approxLocation", "distanceKm" }, first.EnumerateObject().Select(property => property.Name));
        }

        using (var document = JsonDocument.Parse(await accent.Content.ReadAsStringAsync()))
        {
            Assert.Contains(document.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == "Colonia Cuauhtémoc");
        }

        using (var document = JsonDocument.Parse(await broad.Content.ReadAsStringAsync()))
        {
            Assert.True(document.RootElement.GetProperty("items").GetArrayLength() <= SearchService.MaximumPlaces);
        }

        using (var document = JsonDocument.Parse(await none.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, document.RootElement.GetProperty("items").GetArrayLength());
        }

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Demo_seed_is_idempotent_and_keeps_the_property_mapping()
    {
        using var factory = await SeededAsync();
        var seed = factory.Services.GetRequiredService<DemoRoomSeedService>();

        var again = await seed.SeedAsync(CancellationToken.None);

        Assert.Equal(0, again);
        Assert.Equal(12, factory.Buildings.Buildings.Count);
        Assert.Equal(12, factory.Rooms.Rooms.Count);
        Assert.Equal(
            PropertySeedData.Create().Select(record => record.Id).OrderBy(id => id),
            factory.Buildings.Buildings.Values.Select(building => building.DemoSourceId).OrderBy(id => id));
        Assert.All(factory.Buildings.Buildings.Values, building => Assert.Equal(DemoRoomSeedService.SystemLandlordId, building.OwnerId));
    }

    [Fact]
    public async Task Search_limits_and_dependencies_fail_closed()
    {
        using var factory = await SeededAsync(new UserRateLimitOptions { SearchPerWindow = 1 });
        using var client = factory.CreateHttpsClient();
        var uttt = Uttt(factory);

        (await client.GetAsync($"{Rooms}/count?universityId={uttt}")).Dispose();
        using var limited = await client.GetAsync($"{Rooms}/count?universityId={uttt}");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        using var other = await SeededAsync();
        using var otherClient = other.CreateHttpsClient();
        other.Buildings.Unavailable = true;
        using var down = await otherClient.GetAsync($"{Rooms}/search?universityId={Uttt(other)}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
    }

    // --- Soportes -------------------------------------------------

    private static async Task<UserResourceFactory> SeededAsync(UserRateLimitOptions? limits = null)
    {
        var factory = new UserResourceFactory(limits);
        await factory.Services.GetRequiredService<DemoRoomSeedService>().SeedAsync(CancellationToken.None);
        await factory.Services.GetRequiredService<PlaceSeedService>().SeedAsync(CancellationToken.None);
        return factory;
    }

    private static string Uttt(UserResourceFactory factory) =>
        factory.Universities.Universities.Single(university => university.Code == "UTTT").Id;

    private static async Task<long> TotalAsync(HttpClient client, string universityId)
    {
        using var response = await client.GetAsync($"{Rooms}/search?universityId={universityId}&maxDistanceKm=30");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("totalItems").GetInt64();
    }
}
