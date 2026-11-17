using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-19 T01–T06: contrato HTTP de reseñas, elegibilidad, edición, resumen,
// reporte y ocultamiento (NV-21), BOLA, validación y fallo cerrado.
public sealed class ReviewsHttpTests
{
    private const string Mine = "/api/v1/users/me/reviews";

    [Fact]
    public async Task Student_who_visited_reviews_edits_and_sees_the_summary()
    {
        using var factory = new UserResourceFactory();
        var roomId = await RoomOfBobAsync(factory);
        await VisitedAsync(factory, AliceId, roomId);
        using var alice = await factory.ClientForAsync(AliceId);

        using var eligible = await alice.GetAsync($"/api/v1/rooms/{roomId}/reviews/eligibility");
        Assert.Equal(HttpStatusCode.OK, eligible.StatusCode);
        using (var document = JsonDocument.Parse(await eligible.Content.ReadAsStringAsync()))
        {
            Assert.Equal(new[] { "eligible", "basis", "reason", "reviewId", "editableUntil" }, document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.True(document.RootElement.GetProperty("eligible").GetBoolean());
            Assert.Equal("visit", document.RootElement.GetProperty("basis").GetString());
        }

        using var created = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(4, "Muy limpio y cerca"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
        string id;
        using (var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(
                new[] { "id", "room", "author", "basis", "landlord", "property", "landlordRating", "propertyRating", "comment", "edited", "createdAt", "updatedAt" },
                root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "communication", "attention", "compliance", "respect" }, root.GetProperty("landlord").EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "cleanliness", "location", "value", "condition", "services" }, root.GetProperty("property").EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "name" }, root.GetProperty("author").EnumerateObject().Select(property => property.Name));
            id = root.GetProperty("id").GetString()!;
        }

        using var duplicate = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(5));
        await AssertCodeAsync(duplicate, HttpStatusCode.Conflict, "review_exists");

        using var edited = await alice.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { landlord = new { communication = 5, attention = 5, compliance = 5, respect = 5 } });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.True(JsonDocument.Parse(await edited.Content.ReadAsStringAsync()).RootElement.GetProperty("edited").GetBoolean());

        using var list = await alice.GetAsync($"/api/v1/rooms/{roomId}/reviews");
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(new[] { "summary", "items", "page", "pageSize", "totalItems", "totalPages" }, root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "property", "landlord", "criteria" }, root.GetProperty("summary").EnumerateObject().Select(property => property.Name));
            Assert.Equal(5, root.GetProperty("summary").GetProperty("landlord").GetProperty("average").GetDouble());
            Assert.Equal(1, root.GetProperty("summary").GetProperty("property").GetProperty("count").GetInt32());
            Assert.DoesNotContain(AliceId, root.GetRawText());
        }

        using var mine = await alice.GetAsync(Mine);
        Assert.Equal("published", JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Only_students_who_stayed_can_review_and_only_the_author_edits()
    {
        using var factory = new UserResourceFactory();
        var roomId = await RoomOfBobAsync(factory);
        await VisitedAsync(factory, AliceId, roomId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);

        await AssertCodeAsync(await carol.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(5)), HttpStatusCode.Forbidden, "no_stay");
        await AssertCodeAsync(await bob.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(5)), HttpStatusCode.Forbidden, "own_room");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", new { landlord = new { communication = 5 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", new { landlord = new { communication = 5, attention = 5, compliance = 5, respect = 5 }, property = new { cleanliness = 5, location = 5, value = 5, condition = 5, services = 5 }, authorId = CarolId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync("/api/v1/rooms/6a00000000000000000000ff/reviews")).StatusCode);

        using var created = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(4));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await carol.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { comment = "ajeno" })).StatusCode);

        var stored = factory.Reviews.Reviews[id];
        factory.Reviews.Reviews[id] = stored with { CreatedAt = stored.CreatedAt.AddDays(-31) };
        await AssertCodeAsync(await alice.PatchAsJsonAsync($"/api/v1/reviews/{id}", new { comment = "tarde" }), HttpStatusCode.Conflict, "review_locked");

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/rooms/{roomId}/reviews")).StatusCode);
    }

    [Fact]
    public async Task Reviews_are_reported_and_hidden_by_an_admin()
    {
        using var factory = new UserResourceFactory();
        var roomId = await RoomOfBobAsync(factory);
        await VisitedAsync(factory, AliceId, roomId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);
        using var created = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", Body(2, "No me gustó"));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;

        using var report = await bob.PostAsJsonAsync("/api/v1/reports", new { targetType = "review", targetId = id, reason = "inappropriate" });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var reportId = JsonDocument.Parse(await report.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;

        await AssertCodeAsync(await carol.PostAsJsonAsync($"/api/v1/admin/reviews/{id}/hide", new { reason = "Lenguaje ofensivo" }), HttpStatusCode.Forbidden, "role_required");
        await factory.Moderation.GrantAsync(CarolId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);

        using var resolved = await carol.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/resolve", new { action = "hide_review", reason = "Lenguaje ofensivo" });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.Equal(ReviewStatus.Hidden, factory.Reviews.Reviews[id].Status);
        using var hiddenList = await alice.GetAsync($"/api/v1/rooms/{roomId}/reviews");
        Assert.Equal(0, JsonDocument.Parse(await hiddenList.Content.ReadAsStringAsync()).RootElement.GetProperty("totalItems").GetInt64());
        using var mine = await alice.GetAsync(Mine);
        Assert.Equal("hidden", JsonDocument.Parse(await mine.Content.ReadAsStringAsync()).RootElement.GetProperty("items")[0].GetProperty("status").GetString());

        await AssertCodeAsync(await carol.PostAsJsonAsync($"/api/v1/admin/reviews/{id}/hide", new { reason = "Otra vez" }), HttpStatusCode.Conflict, "review_state");
        using var shown = await carol.PostAsJsonAsync($"/api/v1/admin/reviews/{id}/unhide", new { reason = "Revisado" });
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Equal("published", JsonDocument.Parse(await shown.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory();
        var roomId = await RoomOfBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);

        factory.Reviews.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync($"/api/v1/rooms/{roomId}/reviews")).StatusCode);
        factory.Reviews.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync(Mine)).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static object Body(int score, string? comment = null) =>
        new
        {
            landlord = new { communication = score, attention = score, compliance = score, respect = score },
            property = new { cleanliness = score, location = score, value = score, condition = score, services = score },
            comment,
        };

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        }
    }

    private static Task VisitedAsync(UserResourceFactory factory, string userId, string roomId) =>
        factory.Visits.CreateAsync(
            new Visit
            {
                UserId = userId,
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = VisitStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            },
            CancellationToken.None);

    private static async Task<string> RoomOfBobAsync(UserResourceFactory factory)
    {
        var building = await factory.Buildings.CreateAsync(
            new Building
            {
                OwnerId = BobId,
                Name = "Casa Verde",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        return (await factory.Rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = BobId, Number = 101, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None))[0].Id;
    }
}
