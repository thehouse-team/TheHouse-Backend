using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-17 1.3.0: calendario y acciones del arrendador por HTTP, dirección
// exacta solo con la visita confirmada (RNF-02), BOLA, códigos y límites.
public sealed class LandlordVisitsHttpTests
{
    private const string Visits = "/api/v1/visits";
    private const string Calendar = "/api/v1/landlord/visits";
    private const string Phone = "+527711234567";

    [Fact]
    public async Task Landlord_confirms_and_student_then_sees_the_exact_address()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var visitId = await CreateVisitAsync(alice, roomId, Slot(3, 11));

        using (var before = JsonDocument.Parse(await (await alice.GetAsync($"{Visits}/{visitId}")).Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("address").ValueKind);
            Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("location").ValueKind);
            Assert.Equal("Bob", before.RootElement.GetProperty("landlord").GetProperty("name").GetString());
        }

        using var calendar = await bob.GetAsync(Calendar);
        Assert.Equal(HttpStatusCode.OK, calendar.StatusCode);
        Assert.Contains("no-store", calendar.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await calendar.Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(
                new[] { "id", "room", "scheduledAt", "previousScheduledAt", "timeZone", "status", "message", "student", "cancellation", "createdAt", "updatedAt", "confirmedAt", "cancelledAt" },
                item.EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "name", "university" }, item.GetProperty("student").EnumerateObject().Select(property => property.Name));
        }

        using var confirmed = await bob.PostAsync($"{Calendar}/{visitId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using var after = JsonDocument.Parse(await (await alice.GetAsync($"{Visits}/{visitId}")).Content.ReadAsStringAsync());
        var root = after.RootElement;
        Assert.Equal("confirmed", root.GetProperty("status").GetString());
        Assert.Equal(
            new[] { "street", "number", "neighborhood", "city", "state", "postalCode" },
            root.GetProperty("address").EnumerateObject().Select(property => property.Name));
        Assert.Equal("Calle Privada", root.GetProperty("address").GetProperty("street").GetString());
        Assert.Equal(20.0512, root.GetProperty("location").GetProperty("lat").GetDouble());
        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("confirmedAt").ValueKind);

        using var confirmedList = await alice.GetAsync($"/api/v1/users/me/visits?status=confirmed");
        using (var document = JsonDocument.Parse(await confirmedList.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, document.RootElement.GetProperty("totalItems").GetInt64());
        }
    }

    [Fact]
    public async Task Reschedule_and_cancel_by_both_sides()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        var secondRoom = await PublishRoomForBobAsync(factory, 102);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var first = await CreateVisitAsync(alice, roomId, Slot(3, 11));
        var second = await CreateVisitAsync(alice, secondRoom, Slot(4, 11));

        using var moved = await bob.PostAsJsonAsync($"{Calendar}/{first}/reschedule", new { scheduledAt = Slot(5, 12), note = "Mejor ese día" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        using (var document = JsonDocument.Parse(await moved.Content.ReadAsStringAsync()))
        {
            Assert.Equal("confirmed", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(DateTimeOffset.Parse(Slot(3, 11), System.Globalization.CultureInfo.InvariantCulture), document.RootElement.GetProperty("previousScheduledAt").GetDateTimeOffset());
        }

        using var studentCancel = await alice.PostAsJsonAsync($"{Visits}/{first}/cancel", new { reason = "Ya encontré cuarto" });
        using var landlordCancel = await bob.PostAsJsonAsync($"{Calendar}/{second}/cancel", new { reason = "schedule_conflict", note = "Ese día viajo" });

        Assert.Equal("student", await CancelledByAsync(studentCancel));
        Assert.Equal("landlord", await CancelledByAsync(landlordCancel));
        using var seen = JsonDocument.Parse(await (await alice.GetAsync($"{Visits}/{second}")).Content.ReadAsStringAsync());
        Assert.Equal("Ese día viajo", seen.RootElement.GetProperty("cancellation").GetProperty("note").GetString());
        Assert.Equal(JsonValueKind.Null, seen.RootElement.GetProperty("address").ValueKind);
    }

    [Fact]
    public async Task Rules_and_codes_for_landlord_actions()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        var secondRoom = await PublishRoomForBobAsync(factory, 102);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var first = await CreateVisitAsync(alice, roomId, Slot(3, 11));
        await CreateVisitAsync(alice, secondRoom, Slot(4, 11));

        using var outside = await bob.PostAsJsonAsync($"{Calendar}/{first}/reschedule", new { scheduledAt = Slot(5, 21) });
        using var overlap = await bob.PostAsJsonAsync($"{Calendar}/{first}/reschedule", new { scheduledAt = Slot(4, 11, 30) });
        using var badReason = await bob.PostAsJsonAsync($"{Calendar}/{first}/cancel", new { reason = "other" });
        using var massAssignment = await bob.PostAsJsonAsync($"{Calendar}/{first}/cancel", new { reason = "not_available", status = "confirmed" });
        (await bob.PostAsync($"{Calendar}/{first}/confirm", null)).Dispose();
        using var twice = await bob.PostAsync($"{Calendar}/{first}/confirm", null);
        using var wideRange = await bob.GetAsync($"{Calendar}?from={Uri.EscapeDataString(Slot(1, 8))}&to={Uri.EscapeDataString(Slot(70, 8))}");

        await AssertProblemAsync(outside, (HttpStatusCode)422, "outside_hours");
        await AssertProblemAsync(overlap, HttpStatusCode.Conflict, "student_schedule_overlap");
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, massAssignment.StatusCode);
        await AssertProblemAsync(twice, HttpStatusCode.Conflict, "visit_state");
        Assert.Equal(HttpStatusCode.BadRequest, wideRange.StatusCode);
    }

    [Fact]
    public async Task Calendar_and_actions_are_private_to_the_room_owner()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var carol = await factory.ClientForAsync(CarolId);
        var visitId = await CreateVisitAsync(alice, roomId, Slot(3, 11));

        using var noLandlord = await carol.GetAsync(Calendar);
        await MakeLandlordAsync(factory, CarolId, "Carolina Díaz");
        using var foreignCalendar = await carol.GetAsync(Calendar);
        using var foreignDetail = await carol.GetAsync($"{Calendar}/{visitId}");
        using var foreignConfirm = await carol.PostAsync($"{Calendar}/{visitId}/confirm", null);
        using var studentAsLandlord = await alice.PostAsync($"{Calendar}/{visitId}/confirm", null);
        using var anonymous = await factory.CreateHttpsClient().GetAsync(Calendar);

        await AssertProblemAsync(noLandlord, HttpStatusCode.Forbidden, "landlord_required");
        using (var document = JsonDocument.Parse(await foreignCalendar.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, document.RootElement.GetProperty("totalItems").GetInt64());
        }

        Assert.Equal(HttpStatusCode.NotFound, foreignDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignConfirm.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentAsLandlord.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(VisitStatus.Requested, factory.Visits.Visits[visitId].Status);
    }

    [Fact]
    public async Task Rental_request_shows_the_confirmed_visit()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var visitId = await CreateVisitAsync(alice, roomId, Slot(3, 11));
        (await bob.PostAsync($"{Calendar}/{visitId}/confirm", null)).Dispose();

        using var created = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola" });
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(visitId, document.RootElement.GetProperty("visit").GetProperty("id").GetString());
        Assert.Equal("confirmed", document.RootElement.GetProperty("visit").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Landlord_views_never_include_contact_data_or_the_address()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var visitId = await CreateVisitAsync(alice, roomId, Slot(3, 11));
        (await bob.PostAsync($"{Calendar}/{visitId}/confirm", null)).Dispose();

        foreach (var path in new[] { Calendar, $"{Calendar}/{visitId}" })
        {
            var body = await (await bob.GetAsync(path)).Content.ReadAsStringAsync();
            Assert.DoesNotContain(Phone, body, StringComparison.Ordinal);
            Assert.DoesNotContain("@example.com", body, StringComparison.Ordinal);
            Assert.DoesNotContain(AliceId, body, StringComparison.Ordinal);
            Assert.DoesNotContain("Calle Privada", body, StringComparison.Ordinal);
        }

        var search = await (await factory.CreateHttpsClient().GetAsync($"/api/v1/rooms/search?universityId={factory.Universities.Universities[0].Id}&maxDistanceKm=30")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Calle Privada", search, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limits_and_dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { VisitsPerWindow = 1 });
        await PublishRoomForBobAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);

        (await bob.GetAsync(Calendar)).Dispose();
        using var limited = await bob.GetAsync(Calendar);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        using var other = new UserResourceFactory();
        await PublishRoomForBobAsync(other);
        using var bobAgain = await other.ClientForAsync(BobId);
        other.Visits.Unavailable = true;
        using var mongoDown = await bobAgain.GetAsync(Calendar);
        other.Visits.Unavailable = false;
        other.RateLimit.Unavailable = true;
        using var redisDown = await bobAgain.GetAsync(Calendar);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);
    }

    // --- Soportes -------------------------------------------------

    private static async Task MakeLandlordAsync(UserResourceFactory factory, string userId, string fullName) =>
        await factory.Landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = userId,
                FullName = fullName,
                Phone = Phone,
                RentalMode = "individual",
                AcceptedTermsVersion = "1.0",
                AcceptedTermsAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

    private static async Task<string> PublishRoomForBobAsync(UserResourceFactory factory, int number = 101)
    {
        if (await factory.Landlords.GetAsync(BobId, CancellationToken.None) is null)
        {
            await MakeLandlordAsync(factory, BobId, "Bob Pérez");
        }

        var building = await factory.Buildings.CreateAsync(
            new Building
            {
                OwnerId = BobId,
                Name = "Casa Verde",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Latitude = 20.0512,
                Longitude = -99.3401,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        var rooms = await factory.Rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = BobId, Number = number, Floor = 1, Price = 2800, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None);
        return rooms[0].Id;
    }

    private static async Task<string> CreateVisitAsync(HttpClient client, string roomId, string scheduledAt)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Visits)
        {
            Content = JsonContent.Create(new { roomId, scheduledAt }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string?> CancelledByAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("cancelled", document.RootElement.GetProperty("status").GetString());
        return document.RootElement.GetProperty("cancellation").GetProperty("by").GetString();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
    }
}
