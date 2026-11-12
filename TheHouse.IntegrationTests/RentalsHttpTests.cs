using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-18 T01–T07: contrato HTTP del cierre de trato, Mi renta y fin de renta;
// códigos de error, BOLA, límites del código y fallo cerrado.
public sealed class RentalsHttpTests
{
    private const string Landlord = "/api/v1/landlord/agreements";
    private const string Preview = "/api/v1/agreements/preview";
    private const string Accept = "/api/v1/agreements/accept";
    private const string Rental = "/api/v1/users/me/rental";

    [Fact]
    public async Task Landlord_closes_the_deal_student_accepts_and_the_rental_ends()
    {
        using var factory = new UserResourceFactory();
        var (roomId, requestId) = await AcceptedRequestAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        using var created = await bob.PostAsJsonAsync(Landlord, Terms(requestId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
        string agreementId, code, qrToken;
        using (var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(
                new[] { "id", "room", "student", "terms", "status", "closingCode", "createdAt", "updatedAt", "activatedAt", "endedAt", "cancelledAt" },
                root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "monthlyRent", "deposit", "startDate", "durationMonths", "endDate", "paymentDay", "currency" }, root.GetProperty("terms").EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "qrToken", "backupCode", "expiresAt" }, root.GetProperty("closingCode").EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "name", "university" }, root.GetProperty("student").EnumerateObject().Select(property => property.Name));
            Assert.Equal("pending", root.GetProperty("status").GetString());
            Assert.Equal("MXN", root.GetProperty("terms").GetProperty("currency").GetString());
            agreementId = root.GetProperty("id").GetString()!;
            code = root.GetProperty("closingCode").GetProperty("backupCode").GetString()!;
            qrToken = root.GetProperty("closingCode").GetProperty("qrToken").GetString()!;
        }

        Assert.Equal(JsonValueKind.Null, (await JsonAsync(bob, $"{Landlord}/{agreementId}")).GetProperty("closingCode").ValueKind);

        using var preview = await alice.PostAsJsonAsync(Preview, new { code = $"{code[..4].ToLowerInvariant()}-{code[4..]}" });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using (var document = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { "id", "room", "building", "landlord", "terms", "codeExpiresAt" },
                document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal("Bob", document.RootElement.GetProperty("landlord").GetProperty("name").GetString());
            Assert.DoesNotContain("Calle Privada", document.RootElement.GetRawText());
        }

        using var accepted = await alice.PostAsJsonAsync(Accept, new { qrToken, acceptedTerms = true });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using (var document = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(
                new[] { "agreementId", "room", "building", "terms", "nextPayment", "address", "location", "landlord", "conversationId", "activatedAt" },
                root.EnumerateObject().Select(property => property.Name));
            Assert.Equal("Calle Privada", root.GetProperty("address").GetProperty("street").GetString());
            Assert.Equal(new[] { "lat", "lng" }, root.GetProperty("location").EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "dueDate", "amount" }, root.GetProperty("nextPayment").EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain("+52", root.GetRawText());
        }

        Assert.Equal(RoomStatus.Rented, factory.Rooms.Rooms[roomId].Status);
        Assert.Equal("active", (await JsonAsync(bob, $"{Landlord}/{agreementId}")).GetProperty("status").GetString());
        Assert.Equal(agreementId, (await JsonAsync(alice, Rental)).GetProperty("agreementId").GetString());

        using var again = await alice.PostAsJsonAsync(Accept, new { code, acceptedTerms = true });
        await AssertProblemAsync(again, HttpStatusCode.Gone, "code_used");

        using var ended = await bob.PostAsJsonAsync($"{Landlord}/{agreementId}/end", new { reason = "contract_finished" });
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.Equal(RoomStatus.Available, factory.Rooms.Rooms[roomId].Status);
        using var none = await alice.GetAsync(Rental);
        await AssertProblemAsync(none, HttpStatusCode.NotFound, "no_active_rental");
    }

    [Fact]
    public async Task Errors_have_stable_codes()
    {
        using var factory = new UserResourceFactory();
        var (roomId, requestId) = await AcceptedRequestAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);

        using var notLandlord = await alice.PostAsJsonAsync(Landlord, Terms(requestId));
        await AssertProblemAsync(notLandlord, HttpStatusCode.Forbidden, "landlord_required");

        using var badTerms = await bob.PostAsJsonAsync(Landlord, new { requestId, monthlyRent = 100, deposit = 0, startDate = "2026-12-01", durationMonths = 6, paymentDay = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, badTerms.StatusCode);
        using var massAssignment = await bob.PostAsJsonAsync(Landlord, new { requestId, monthlyRent = 3000, deposit = 0, startDate = Start(), durationMonths = 6, paymentDay = 5, studentId = CarolId });
        Assert.Equal(HttpStatusCode.BadRequest, massAssignment.StatusCode);

        var created = await CreateAsync(bob, requestId);
        using var duplicate = await bob.PostAsJsonAsync(Landlord, Terms(requestId));
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "agreement_open");

        using var foreign = await carol.PostAsJsonAsync(Preview, new { code = created.Code });
        await AssertProblemAsync(foreign, HttpStatusCode.NotFound, "code_invalid");
        using var noTerms = await alice.PostAsJsonAsync(Accept, new { code = created.Code, acceptedTerms = false });
        Assert.Equal(HttpStatusCode.BadRequest, noTerms.StatusCode);

        var stored = factory.Agreements.Agreements[created.Id];
        factory.Agreements.Agreements[created.Id] = stored with { Code = stored.Code! with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) } };
        using var expired = await alice.PostAsJsonAsync(Accept, new { code = created.Code, acceptedTerms = true });
        await AssertProblemAsync(expired, HttpStatusCode.Gone, "code_expired");
        Assert.Equal(RoomStatus.Available, factory.Rooms.Rooms[roomId].Status);

        using var reissued = await bob.PostAsync($"{Landlord}/{created.Id}/code", null);
        Assert.Equal(HttpStatusCode.OK, reissued.StatusCode);
        using var cancelled = await bob.PostAsync($"{Landlord}/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using var cancelAgain = await bob.PostAsync($"{Landlord}/{created.Id}/cancel", null);
        await AssertProblemAsync(cancelAgain, HttpStatusCode.Conflict, "agreement_state");
    }

    [Fact]
    public async Task Agreements_are_private_to_their_landlord_and_student()
    {
        using var factory = new UserResourceFactory();
        var (_, requestId) = await AcceptedRequestAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);
        await MakeLandlordAsync(factory, CarolId, "Carol Díaz");
        var created = await CreateAsync(bob, requestId);

        using var detail = await carol.GetAsync($"{Landlord}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        using var end = await carol.PostAsync($"{Landlord}/{created.Id}/end", null);
        Assert.Equal(HttpStatusCode.NotFound, end.StatusCode);
        using var list = await carol.GetAsync(Landlord);
        Assert.Equal(0, JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("totalItems").GetInt64());
        using var fromOthers = await carol.PostAsJsonAsync(Landlord, Terms(requestId));
        Assert.Equal(HttpStatusCode.NotFound, fromOthers.StatusCode);

        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync(Rental);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Closing_code_attempts_are_limited_per_account()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await carol.PostAsJsonAsync(Preview, new { code = "ABCD2345" });
            await AssertProblemAsync(response, HttpStatusCode.NotFound, "code_invalid");
        }

        using var blocked = await carol.PostAsJsonAsync(Preview, new { code = "ABCD2345" });
        await AssertProblemAsync(blocked, HttpStatusCode.TooManyRequests, "too_many_attempts");
        Assert.Equal("900", blocked.Headers.RetryAfter?.ToString());
    }

    [Fact]
    public async Task Dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory();
        var (_, requestId) = await AcceptedRequestAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        factory.Agreements.Unavailable = true;
        using var mongo = await bob.PostAsJsonAsync(Landlord, Terms(requestId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongo.StatusCode);
        factory.Agreements.Unavailable = false;

        factory.RateLimit.Unavailable = true;
        using var redis = await alice.GetAsync(Rental);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redis.StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static string Start() => DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static object Terms(string requestId) =>
        new { requestId, monthlyRent = 3000, deposit = 3000, startDate = Start(), durationMonths = 6, paymentDay = 5 };

    private static async Task<(string Id, string Code)> CreateAsync(HttpClient bob, string requestId)
    {
        using var response = await bob.PostAsJsonAsync(Landlord, Terms(requestId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (document.RootElement.GetProperty("id").GetString()!, document.RootElement.GetProperty("closingCode").GetProperty("backupCode").GetString()!);
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
    }

    // Bob publica un cuarto, Alice pide informes y Bob acepta (NV-16).
    private static async Task<(string RoomId, string RequestId)> AcceptedRequestAsync(UserResourceFactory factory)
    {
        await MakeLandlordAsync(factory, BobId, "Bob Pérez");
        var building = await factory.Buildings.CreateAsync(
            new Building
            {
                OwnerId = BobId,
                Name = "Casa Verde",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Latitude = 20.05,
                Longitude = -99.34,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        var room = (await factory.Rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = BobId, Number = 101, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None))[0];

        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var request = await alice.PostAsync(
            $"/api/v1/rooms/{room.Id}/requests",
            new StringContent("{\"message\":\"Hola, me interesa\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var requestId = JsonDocument.Parse(await request.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        using var accepted = await bob.PostAsync($"/api/v1/landlord/requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        return (room.Id, requestId);
    }

    private static Task MakeLandlordAsync(UserResourceFactory factory, string userId, string fullName) =>
        factory.Landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = userId,
                FullName = fullName,
                Phone = "+527711234567",
                RentalMode = "individual",
                AcceptedTermsVersion = "1.0",
                AcceptedTermsAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);
}
