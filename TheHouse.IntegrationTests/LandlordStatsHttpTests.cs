using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-25 T01–T05: vistas desde el detalle real del cuarto, resumen, por
// cuarto y sugerencias por HTTP; BOLA, validación, límites y fallo cerrado.
public sealed class LandlordStatsHttpTests
{
    private const string Stats = "/api/v1/landlord/stats";

    [Fact]
    public async Task Student_activity_appears_in_the_landlord_stats()
    {
        using var factory = new UserResourceFactory();
        var (buildingId, roomId) = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);

        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await carol.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        Assert.Equal(2, factory.RoomViews.Items.Count);
        Assert.DoesNotContain(factory.RoomViews.Items, item => item.ActorHash.Contains(AliceId, StringComparison.Ordinal));

        Assert.True((await alice.PutAsync($"/api/v1/users/me/favorites/{roomId}", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola, ¿sigue disponible?" })).StatusCode);

        using var summary = await bob.GetAsync(Stats);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        Assert.Contains("no-store", summary.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await summary.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(["from", "to", "buildingId", "totals", "previous", "daily", "favoritesNow", "updatedAt"], root.EnumerateObject().Select(property => property.Name));
            var totals = root.GetProperty("totals");
            Assert.Equal(["views", "favorites", "requests", "acceptedRequests", "visits", "confirmedVisits", "agreements", "conversion"], totals.EnumerateObject().Select(property => property.Name));
            Assert.Equal(2, totals.GetProperty("views").GetInt32());
            Assert.Equal(1, totals.GetProperty("favorites").GetInt32());
            Assert.Equal(1, totals.GetProperty("requests").GetInt32());
            Assert.Equal(0.5, totals.GetProperty("conversion").GetDouble());
            Assert.Equal(30, root.GetProperty("daily").GetArrayLength());
            Assert.Equal(2, root.GetProperty("daily")[29].GetProperty("views").GetInt32());
            Assert.Equal(1, root.GetProperty("favoritesNow").GetInt32());
        }

        using var rooms = await bob.GetAsync($"{Stats}/rooms?buildingId={buildingId}");
        Assert.Equal(HttpStatusCode.OK, rooms.StatusCode);
        using (var document = JsonDocument.Parse(await rooms.Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(
                ["roomId", "buildingId", "buildingName", "number", "status", "price", "views", "favorites", "requests", "visits", "agreements", "conversion", "rating"],
                item.EnumerateObject().Select(property => property.Name));
            Assert.Equal(roomId, item.GetProperty("roomId").GetString());
            Assert.Equal("available", item.GetProperty("status").GetString());
            Assert.Equal(2, item.GetProperty("views").GetInt32());
        }

        using var tips = await bob.GetAsync($"{Stats}/recommendations");
        Assert.Equal(HttpStatusCode.OK, tips.StatusCode);
        using (var document = JsonDocument.Parse(await tips.Content.ReadAsStringAsync()))
        {
            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(["code", "priority", "buildingId", "roomId", "title", "detail"], items[0].EnumerateObject().Select(property => property.Name));
            Assert.Contains(items, item => item.GetProperty("code").GetString() == "add_photos");
            Assert.Contains(items, item => item.GetProperty("code").GetString() == "missing_details");
        }

        var raw = await (await bob.GetAsync(Stats)).Content.ReadAsStringAsync() + await (await bob.GetAsync($"{Stats}/rooms")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(AliceId, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(CarolId, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stats_are_only_for_landlords_and_their_buildings()
    {
        using var factory = new UserResourceFactory();
        await PublishRoomForBobAsync(factory);
        var (carolBuilding, _) = await PublishRoomForAsync(factory, CarolId, "Carol Díaz");
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        await AssertCodeAsync(await alice.GetAsync(Stats), HttpStatusCode.Forbidden, "landlord_required");
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"{Stats}?buildingId={carolBuilding}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"{Stats}/recommendations?buildingId={carolBuilding}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"{Stats}?buildingId=nope")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"{Stats}?from=2026-01-01&to=2026-06-30")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync($"{Stats}/rooms?pageSize=51")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Stats)).StatusCode);
    }

    [Fact]
    public async Task Views_never_break_the_room_detail_and_stats_fail_closed()
    {
        using var factory = new UserResourceFactory();
        var (_, roomId) = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        factory.ViewSalts.Unavailable = true;
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        factory.ViewSalts.Unavailable = false;
        factory.RoomViews.Unavailable = true;
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        Assert.Empty(factory.RoomViews.Items);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await bob.GetAsync(Stats)).StatusCode);
        factory.RoomViews.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await bob.GetAsync(Stats)).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static Task<(string BuildingId, string RoomId)> PublishRoomForBobAsync(UserResourceFactory factory) =>
        PublishRoomForAsync(factory, BobId, "Bob Pérez");

    private static async Task<(string BuildingId, string RoomId)> PublishRoomForAsync(UserResourceFactory factory, string ownerId, string fullName)
    {
        await factory.Landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = ownerId,
                FullName = fullName,
                Phone = "+527711234567",
                RentalMode = "individual",
                AcceptedTermsVersion = "1.0",
                AcceptedTermsAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);
        var building = await factory.Buildings.CreateAsync(
            new Building
            {
                OwnerId = ownerId,
                Name = "Casa Verde",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Latitude = 20.0512,
                Longitude = -99.3401,
                NearestUniversityId = factory.Universities.Universities.Single(university => university.Code == "UTTT").Id,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        var rooms = await factory.Rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = ownerId, Number = 101, Floor = 1, Price = 2800, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None);
        return (building.Id, rooms[0].Id);
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        }
    }
}
