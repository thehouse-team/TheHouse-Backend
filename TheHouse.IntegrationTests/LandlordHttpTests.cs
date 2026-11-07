using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-12 T01–T11: arrendador, edificios y cuartos por HTTP con dos
// arrendadores y un usuario sin perfil; estados, versiones, límites,
// BOLA, fallo cerrado y logs sin datos personales.
public sealed class LandlordHttpTests
{
    private const string Base = "/api/v1/landlord";
    private const string Phone = "+527731234567";

    [Fact]
    public async Task Landlord_profile_is_created_once_and_validated()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var before = await alice.GetAsync($"{Base}/profile");
        using var created = await alice.PostAsJsonAsync($"{Base}/profile", Profile());
        using var again = await alice.PostAsJsonAsync($"{Base}/profile", Profile());
        using var read = await alice.GetAsync($"{Base}/profile");

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { "fullName", "phone", "rentalMode", "rfc", "acceptedTermsVersion", "createdAt" },
                document.RootElement.EnumerateObject().Select(property => property.Name));
        }

        await AssertCodeAsync(again, HttpStatusCode.Conflict, "landlord_exists");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Theory]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"7731234567\",\"rentalMode\":\"individual\",\"acceptedTermsVersion\":\"2026-11\"}")]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"+527731234567\",\"rentalMode\":\"agency\",\"acceptedTermsVersion\":\"2026-11\"}")]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"+527731234567\",\"rentalMode\":\"agency\",\"rfc\":\"SAT970701NN4\",\"acceptedTermsVersion\":\"2026-11\"}")]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"+527731234567\",\"rentalMode\":\"individual\",\"acceptedTermsVersion\":\"2025-01\"}")]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"+527731234567\",\"rentalMode\":\"boss\",\"acceptedTermsVersion\":\"2026-11\"}")]
    [InlineData("{\"fullName\":\"Ana\",\"phone\":\"+527731234567\",\"rentalMode\":\"individual\",\"acceptedTermsVersion\":\"2026-11\",\"userId\":\"x\"}")]
    public async Task Landlord_profile_rejects_invalid_data(string body)
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PostAsync($"{Base}/profile", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Landlords.Profiles);
    }

    [Fact]
    public async Task Agency_with_valid_rfc_is_accepted()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PostAsJsonAsync($"{Base}/profile", new
        {
            fullName = "Inmobiliaria Tula",
            phone = Phone,
            rentalMode = "agency",
            rfc = "SAT970701NN3",
            acceptedTermsVersion = "2026-11",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Users_without_landlord_profile_get_403()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);

        using var list = await carol.GetAsync($"{Base}/buildings");
        using var create = await carol.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory));

        await AssertCodeAsync(list, HttpStatusCode.Forbidden, "landlord_required");
        await AssertCodeAsync(create, HttpStatusCode.Forbidden, "landlord_required");
        Assert.Empty(factory.Buildings.Buildings);
    }

    [Fact]
    public async Task Building_is_created_in_draft_with_computed_distance()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);

        using var response = await alice.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(
            new[] { "id", "type", "name", "address", "location", "nearestUniversity", "distanceToUniversityKm", "walkingMinutes", "allowedTenants", "services", "rules", "status", "roomsTotal", "roomsAvailable", "version", "createdAt", "updatedAt" },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("draft", root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt64());
        Assert.Equal("UTTT", root.GetProperty("nearestUniversity").GetProperty("code").GetString());
        Assert.InRange(root.GetProperty("distanceToUniversityKm").GetDouble(), 0.5, 1.5);
        Assert.InRange(root.GetProperty("walkingMinutes").GetInt32(), 6, 18);
    }

    [Theory]
    [InlineData("location", "{\"lat\":40.7,\"lng\":-74.0}")]
    [InlineData("services", "[\"pool\"]")]
    [InlineData("status", "\"verified\"")]
    [InlineData("ownerId", "\"665f1c2a9b1e4a0000000b0b\"")]
    [InlineData("distanceToUniversityKm", "0")]
    [InlineData("type", "\"castle\"")]
    public async Task Building_rejects_invalid_or_server_owned_fields(string field, string json)
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var body = JsonSerializer.SerializeToNode(BuildingBody(factory))!.AsObject();
        body[field] = System.Text.Json.Nodes.JsonNode.Parse(json);

        using var response = await alice.PostAsync($"{Base}/buildings", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Buildings.Buildings);
    }

    [Fact]
    public async Task Unknown_university_returns_404()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var body = JsonSerializer.SerializeToNode(BuildingBody(factory))!.AsObject();
        body["nearestUniversityId"] = "ffffffffffffffffffffffff";

        using var response = await alice.PostAsync($"{Base}/buildings", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rooms_are_generated_by_floor_without_duplicates_or_excess()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);

        using var generated = await GenerateAsync(alice, building, new[] { 3, 2 }, complete: true);
        using var duplicate = await GenerateAsync(alice, building, new[] { 1 }, complete: true);
        using var tooMany = await GenerateAsync(alice, building, Enumerable.Repeat(7, 29).ToArray(), complete: true);
        using var mismatch = await alice.PostAsJsonAsync($"{Base}/buildings/{building}/rooms/generate", new { floors = 2, roomsPerFloor = new[] { 1 } });
        using var zeroFloors = await alice.PostAsJsonAsync($"{Base}/buildings/{building}/rooms/generate", new { floors = 0, roomsPerFloor = Array.Empty<int>() });
        Assert.Equal(HttpStatusCode.BadRequest, zeroFloors.StatusCode);

        Assert.Equal(HttpStatusCode.Created, generated.StatusCode);
        using (var document = JsonDocument.Parse(await generated.Content.ReadAsStringAsync()))
        {
            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(new[] { 101, 102, 103, 201, 202 }, items.Select(item => item.GetProperty("number").GetInt32()));
            Assert.All(items, item => Assert.Equal("ready", item.GetProperty("status").GetString()));
            Assert.Equal(
                new[] { "id", "buildingId", "number", "floor", "price", "currency", "sizeM2", "type", "bathroom", "furniture", "availableFrom", "minContract", "status", "version", "createdAt", "updatedAt" },
                items[0].EnumerateObject().Select(property => property.Name));
        }

        await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "room_number_taken");
        await AssertCodeAsync(tooMany, HttpStatusCode.Conflict, "rooms_limit");
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal(5, factory.Rooms.Rooms.Count);
    }

    [Fact]
    public async Task Room_edits_require_current_version_and_derive_status()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 1 }, complete: false)).Dispose();
        var room = factory.Rooms.Rooms.Values.Single();
        Assert.Equal(RoomStatus.Setup, room.Status);

        using var completed = await alice.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new
        {
            price = 2800,
            sizeM2 = 12.5m,
            type = "single",
            bathroom = "private",
            availableFrom = "2026-12-01",
            minContract = "six_months",
            version = 1,
        }));
        using var stale = await alice.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new { price = 3000, version = 1 }));
        using var missing = await alice.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new { price = 3000 }));
        using var badPrice = await alice.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new { price = 100, version = 2 }));

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using (var document = JsonDocument.Parse(await completed.Content.ReadAsStringAsync()))
        {
            Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("version").GetInt64());
            Assert.Equal("2026-12-01", document.RootElement.GetProperty("availableFrom").GetString());
        }

        await AssertCodeAsync(stale, HttpStatusCode.Conflict, "version_conflict");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPrice.StatusCode);
    }

    [Fact]
    public async Task Concurrent_edits_with_the_same_version_let_only_one_win()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 1 }, complete: true)).Dispose();
        var room = factory.Rooms.Rooms.Values.Single();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(index =>
                alice.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new { price = 3000 + index, version = 1 }))));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        Assert.Equal(2, factory.Rooms.Rooms[room.Id].Version);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Duplicate_copy_pause_and_resume_follow_the_state_machine()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 2 }, complete: true)).Dispose();
        var rooms = factory.Rooms.Rooms.Values.OrderBy(room => room.Number).ToArray();

        using var duplicated = await alice.PostAsync($"{Base}/rooms/{rooms[0].Id}/duplicate", null);
        using var pauseReady = await alice.PostAsync($"{Base}/rooms/{rooms[0].Id}/pause", null);

        Assert.Equal(HttpStatusCode.Created, duplicated.StatusCode);
        using (var document = JsonDocument.Parse(await duplicated.Content.ReadAsStringAsync()))
        {
            Assert.Equal(103, document.RootElement.GetProperty("number").GetInt32());
        }

        await AssertCodeAsync(pauseReady, HttpStatusCode.Conflict, "room_state");

        // NV-21 todavía no existe: se simula un cuarto aprobado.
        var approved = factory.Rooms.Rooms[rooms[1].Id] with { Status = RoomStatus.Available };
        factory.Rooms.Rooms[approved.Id] = approved;
        using var paused = await alice.PostAsync($"{Base}/rooms/{approved.Id}/pause", null);
        using var resumed = await alice.PostAsync($"{Base}/rooms/{approved.Id}/resume", null);
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(RoomStatus.Available, factory.Rooms.Rooms[approved.Id].Status);
    }

    [Fact]
    public async Task Copy_from_takes_data_from_a_room_of_the_same_building()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 1 }, complete: true)).Dispose();
        var source = factory.Rooms.Rooms.Values.Single();
        using var blank = await alice.PostAsync($"{Base}/rooms/{source.Id}/duplicate", null);
        var target = factory.Rooms.Rooms.Values.Single(room => room.Id != source.Id);
        factory.Rooms.Rooms[target.Id] = target with { Price = null, Status = RoomStatus.Setup };

        using var copied = await alice.PostAsJsonAsync($"{Base}/rooms/{target.Id}/copy-from", new { sourceRoomId = source.Id, version = target.Version });

        Assert.Equal(HttpStatusCode.OK, copied.StatusCode);
        Assert.Equal(source.Price, factory.Rooms.Rooms[target.Id].Price);
        Assert.Equal(RoomStatus.Ready, factory.Rooms.Rooms[target.Id].Status);
    }

    [Fact]
    public async Task Publication_lists_steps_and_publish_moves_to_review()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);

        using var early = await alice.PostAsync($"{Base}/buildings/{building}/publish", null);
        await AssertCodeAsync(early, HttpStatusCode.Conflict, "publication_incomplete");
        using (var document = JsonDocument.Parse(await early.Content.ReadAsStringAsync()))
        {
            Assert.Contains("room_ready", document.RootElement.GetProperty("detail").GetString());
        }

        (await GenerateAsync(alice, building, new[] { 2 }, complete: true)).Dispose();
        using var steps = await alice.GetAsync($"{Base}/buildings/{building}/publication");
        using (var document = JsonDocument.Parse(await steps.Content.ReadAsStringAsync()))
        {
            Assert.True(document.RootElement.GetProperty("canPublish").GetBoolean());
            Assert.Equal(
                new[] { "building_data", "location", "services", "room_ready" },
                document.RootElement.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetString()));
        }

        using var published = await alice.PostAsync($"{Base}/buildings/{building}/publish", null);
        Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);
        Assert.Equal(BuildingStatus.InReview, factory.Buildings.Buildings[building].Status);
        Assert.All(factory.Rooms.Rooms.Values, room => Assert.Equal(RoomStatus.InReview, room.Status));

        using var generate = await GenerateAsync(alice, building, new[] { 1 }, complete: true);
        using var patch = await alice.PatchAsync($"{Base}/buildings/{building}", JsonContent.Create(new { name = "Otro", version = factory.Buildings.Buildings[building].Version }));
        using var again = await alice.PostAsync($"{Base}/buildings/{building}/publish", null);
        await AssertCodeAsync(generate, HttpStatusCode.Conflict, "building_in_review");
        await AssertCodeAsync(patch, HttpStatusCode.Conflict, "building_in_review");
        await AssertCodeAsync(again, HttpStatusCode.Conflict, "building_state");
    }

    [Fact]
    public async Task Other_landlords_never_see_or_change_my_buildings_and_rooms()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        using var bob = await LandlordAsync(factory, BobId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 1 }, complete: true)).Dispose();
        var room = factory.Rooms.Rooms.Values.Single();

        using var get = await bob.GetAsync($"{Base}/buildings/{building}");
        using var rooms = await bob.GetAsync($"{Base}/buildings/{building}/rooms");
        using var patch = await bob.PatchAsync($"{Base}/rooms/{room.Id}", JsonContent.Create(new { price = 9000, version = 1 }));
        using var delete = await bob.DeleteAsync($"{Base}/buildings/{building}?version=1");
        using var publish = await bob.PostAsync($"{Base}/buildings/{building}/publish", null);
        using var list = await bob.GetAsync($"{Base}/buildings");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rooms.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(2800, factory.Rooms.Rooms[room.Id].Price);
    }

    [Fact]
    public async Task Dashboard_summarizes_rooms_and_income_and_delete_respects_rented_rooms()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        (await GenerateAsync(alice, building, new[] { 4 }, complete: true)).Dispose();
        var rooms = factory.Rooms.Rooms.Values.OrderBy(room => room.Number).ToArray();
        factory.Rooms.Rooms[rooms[0].Id] = rooms[0] with { Status = RoomStatus.Rented };
        factory.Rooms.Rooms[rooms[1].Id] = rooms[1] with { Status = RoomStatus.Available };

        using var list = await alice.GetAsync($"{Base}/buildings");
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var summary = document.RootElement.GetProperty("summary");
            Assert.Equal(1, summary.GetProperty("buildings").GetInt32());
            Assert.Equal(4, summary.GetProperty("rooms").GetInt32());
            Assert.Equal(1, summary.GetProperty("available").GetInt32());
            Assert.Equal(25, summary.GetProperty("occupancyPercent").GetInt32());
            Assert.Equal(2800, summary.GetProperty("monthlyIncome").GetInt64());
            var item = document.RootElement.GetProperty("items")[0];
            Assert.Equal(4, item.GetProperty("roomsTotal").GetInt32());
            Assert.Equal(1, item.GetProperty("roomsAvailable").GetInt32());
        }

        using var deleteRented = await alice.DeleteAsync($"{Base}/rooms/{rooms[0].Id}?version={rooms[0].Version}");
        using var deleteBuilding = await alice.DeleteAsync($"{Base}/buildings/{building}?version=1");
        using var deleteNoVersion = await alice.DeleteAsync($"{Base}/rooms/{rooms[2].Id}");
        using var deleteRoom = await alice.DeleteAsync($"{Base}/rooms/{rooms[2].Id}?version={rooms[2].Version}");

        await AssertCodeAsync(deleteRented, HttpStatusCode.Conflict, "room_rented");
        await AssertCodeAsync(deleteBuilding, HttpStatusCode.Conflict, "building_has_rented_rooms");
        Assert.Equal(HttpStatusCode.BadRequest, deleteNoVersion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteRoom.StatusCode);

        factory.Rooms.Rooms[rooms[0].Id] = rooms[0];
        using var deleteAll = await alice.DeleteAsync($"{Base}/buildings/{building}?version=1");
        Assert.Equal(HttpStatusCode.NoContent, deleteAll.StatusCode);
        Assert.Empty(factory.Rooms.Rooms);
    }

    [Fact]
    public async Task Building_update_recomputes_distance_and_checks_version()
    {
        using var factory = new UserResourceFactory();
        using var alice = await LandlordAsync(factory, AliceId);
        var building = await CreateBuildingAsync(alice, factory);
        var tepeji = factory.Universities.Universities.Single(university => university.Code == "UAEH-ESTE");

        using var updated = await alice.PatchAsync($"{Base}/buildings/{building}", JsonContent.Create(new { nearestUniversityId = tepeji.Id, version = 1 }));
        using var stale = await alice.PatchAsync($"{Base}/buildings/{building}", JsonContent.Create(new { name = "Nuevo", version = 1 }));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using (var document = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()))
        {
            Assert.Equal("UAEH-ESTE", document.RootElement.GetProperty("nearestUniversity").GetProperty("code").GetString());
            Assert.InRange(document.RootElement.GetProperty("distanceToUniversityKm").GetDouble(), 9, 12);
            Assert.Equal(2, document.RootElement.GetProperty("version").GetInt64());
        }

        await AssertCodeAsync(stale, HttpStatusCode.Conflict, "version_conflict");
    }

    [Fact]
    public async Task Limits_auth_and_dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { LandlordWritesPerWindow = 2 });
        using var alice = await factory.ClientForAsync(AliceId);
        using var anonymous = factory.CreateHttpsClient();

        (await alice.PostAsJsonAsync($"{Base}/profile", Profile())).Dispose();
        (await alice.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory))).Dispose();
        using var limited = await alice.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory));
        using var noToken = await anonymous.GetAsync($"{Base}/buildings");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        factory.Buildings.Unavailable = true;
        using var down = await alice.GetAsync($"{Base}/buildings");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
    }

    [Fact]
    public async Task Personal_data_is_not_written_to_logs()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        (await alice.PostAsJsonAsync($"{Base}/profile", new
        {
            fullName = "Inmobiliaria Secreta",
            phone = Phone,
            rentalMode = "agency",
            rfc = "SAT970701NN3",
            acceptedTermsVersion = "2026-11",
        })).Dispose();
        (await alice.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory))).Dispose();

        Assert.NotEmpty(factory.Logs);
        Assert.DoesNotContain(factory.Logs, line =>
            line.Contains(Phone, StringComparison.Ordinal) ||
            line.Contains("SAT970701NN3", StringComparison.Ordinal) ||
            line.Contains("Calle Secreta", StringComparison.Ordinal) ||
            line.Contains("Inmobiliaria Secreta", StringComparison.Ordinal));
    }

    // --- Soportes -------------------------------------------------

    private static object Profile() => new
    {
        fullName = "Ana Arrendadora",
        phone = Phone,
        rentalMode = "individual",
        acceptedTermsVersion = "2026-11",
    };

    private static object BuildingBody(UserResourceFactory factory) => new
    {
        type = "rooms_building",
        name = "Casa Estudiantes Tula",
        address = new { street = "Calle Secreta", number = "12", neighborhood = "Centro", city = "Tula de Allende", state = "Hidalgo", postalCode = "42800" },
        location = new { lat = 20.0150, lng = -99.3420 },
        nearestUniversityId = factory.Universities.Universities.Single(university => university.Code == "UTTT").Id,
        allowedTenants = "any",
        services = new[] { "wifi", "water" },
        rules = "Sin fiestas después de las 22:00.",
    };

    private static async Task<HttpClient> LandlordAsync(UserResourceFactory factory, string userId)
    {
        var client = await factory.ClientForAsync(userId);
        using var response = await client.PostAsJsonAsync($"{Base}/profile", Profile());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return client;
    }

    private static async Task<string> CreateBuildingAsync(HttpClient client, UserResourceFactory factory)
    {
        using var response = await client.PostAsJsonAsync($"{Base}/buildings", BuildingBody(factory));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> GenerateAsync(HttpClient client, string building, int[] perFloor, bool complete) =>
        client.PostAsJsonAsync($"{Base}/buildings/{building}/rooms/generate", new
        {
            floors = perFloor.Length,
            roomsPerFloor = perFloor,
            numbering = "floor_prefix",
            @base = complete ? CompleteBase() : null,
        });

    private static object CompleteBase() => new
    {
        price = 2800,
        sizeM2 = 12m,
        type = "single",
        bathroom = "shared",
        furniture = new[] { "bed", "desk" },
        availableFrom = "2026-12-01",
        minContract = "six_months",
    };

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }
}
