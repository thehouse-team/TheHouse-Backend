using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-20 T01–T07: avisos generados por las acciones reales (revisión de
// edificio, solicitud, aceptación, chat y visita), bandeja, contador con 304,
// marcas de leído, BOLA, validación y fallo que no rompe la acción.
public sealed class NotificationsHttpTests
{
    private const string Inbox = "/api/v1/users/me/notifications";
    private const string Count = Inbox + "/unread-count";

    [Fact]
    public async Task Actions_create_notifications_for_the_other_party()
    {
        using var factory = new UserResourceFactory();
        var (alice, roomId) = await ApprovedRoomOwnedByAliceAsync(factory);
        using var _ = alice;
        using var carol = await factory.ClientForAsync(CarolId);

        using var request = await carol.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola, me interesa" });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        string requestId, conversationId;
        using (var document = JsonDocument.Parse(await request.Content.ReadAsStringAsync()))
        {
            requestId = document.RootElement.GetProperty("id").GetString()!;
            conversationId = document.RootElement.GetProperty("conversationId").GetString()!;
        }

        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsync($"/api/v1/landlord/requests/{requestId}/accept", null)).StatusCode);
        var lastMessage = string.Empty;
        foreach (var text in new[] { "¿Puedo ir mañana?", "Gracias" })
        {
            using var sent = await carol.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { text, clientMessageId = Guid.NewGuid().ToString() });
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
            lastMessage = JsonDocument.Parse(await sent.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        }

        using var visit = new HttpRequestMessage(HttpMethod.Post, "/api/v1/visits") { Content = JsonContent.Create(new { roomId, scheduledAt = Slot(3, 11) }) };
        visit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, (await carol.SendAsync(visit)).StatusCode);

        using var aliceInbox = await alice.GetAsync(Inbox);
        Assert.Equal(HttpStatusCode.OK, aliceInbox.StatusCode);
        Assert.Contains("no-store", aliceInbox.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await aliceInbox.Content.ReadAsStringAsync()))
        {
            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(
                new[] { "id", "type", "category", "title", "body", "target", "count", "read", "createdAt", "updatedAt" },
                items[0].EnumerateObject().Select(property => property.Name));
            var types = items.Select(item => item.GetProperty("type").GetString()).ToArray();
            Assert.Equal(new[] { "visit_requested", "message_received", "request_received", "building_approved" }, types);
            var message = items.Single(item => item.GetProperty("type").GetString() == "message_received");
            Assert.Equal(2, message.GetProperty("count").GetInt32());
            Assert.Equal("conversation", message.GetProperty("target").GetProperty("kind").GetString());
            var raw = document.RootElement.GetRawText();
            Assert.DoesNotContain("Puedo", raw);
            Assert.DoesNotContain("Calle 1", raw);
            Assert.DoesNotContain("+52", raw);
            Assert.DoesNotContain(CarolId, raw);
        }

        using var carolInbox = await carol.GetAsync($"{Inbox}?category=requests");
        var accepted = Assert.Single(JsonDocument.Parse(await carolInbox.Content.ReadAsStringAsync()).RootElement.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("request_accepted", accepted.GetProperty("type").GetString());
        Assert.Equal(requestId, accepted.GetProperty("target").GetProperty("id").GetString());

        using var read = await alice.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/read", new { lastMessageId = lastMessage });
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.True(factory.Notifications.Items.Single(item => item.Type == NotificationTypes.MessageReceived).IsRead);
    }

    [Fact]
    public async Task Counter_supports_etag_and_marks_change_it()
    {
        using var factory = new UserResourceFactory();
        Seed(factory, BobId, NotificationTypes.RequestReceived, "requests");
        Seed(factory, BobId, NotificationTypes.VisitRequested, "visits");
        var target = Seed(factory, BobId, NotificationTypes.BuildingApproved, "system");
        using var bob = await factory.ClientForAsync(BobId);

        using var first = await bob.GetAsync(Count);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(first.Headers.CacheControl?.Private);
        Assert.True(first.Headers.CacheControl?.NoCache);
        Assert.False(first.Headers.CacheControl?.Public);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        using (var document = JsonDocument.Parse(await first.Content.ReadAsStringAsync()))
        {
            Assert.Equal(new[] { "unread", "requests", "visits", "system" }, document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(3, document.RootElement.GetProperty("unread").GetInt32());
        }

        using var again = new HttpRequestMessage(HttpMethod.Get, Count);
        again.Headers.IfNoneMatch.Add(etag!);
        using var notModified = await bob.SendAsync(again);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"{Inbox}/{target}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"{Inbox}/{target}/read", null)).StatusCode);
        using var afterOne = new HttpRequestMessage(HttpMethod.Get, Count);
        afterOne.Headers.IfNoneMatch.Add(etag!);
        using var changed = await bob.SendAsync(afterOne);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(2, JsonDocument.Parse(await changed.Content.ReadAsStringAsync()).RootElement.GetProperty("unread").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsJsonAsync($"{Inbox}/read-all", new { category = "visits" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"{Inbox}/read-all", null)).StatusCode);
        using var none = await bob.GetAsync($"{Inbox}?unread=true");
        Assert.Equal(0, JsonDocument.Parse(await none.Content.ReadAsStringAsync()).RootElement.GetProperty("totalItems").GetInt64());
    }

    [Fact]
    public async Task Notifications_are_private_and_inputs_are_strict()
    {
        using var factory = new UserResourceFactory();
        var bobs = Seed(factory, BobId, NotificationTypes.RequestReceived, "requests");
        using var carol = await factory.ClientForAsync(CarolId);

        using var foreign = await carol.PostAsync($"{Inbox}/{bobs}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.False(factory.Notifications.Items.Single().IsRead);
        using var list = await carol.GetAsync(Inbox);
        Assert.Equal(0, JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("totalItems").GetInt64());

        Assert.Equal(HttpStatusCode.BadRequest, (await carol.GetAsync($"{Inbox}?unread=si")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await carol.GetAsync($"{Inbox}?category=pagos")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await carol.PostAsJsonAsync($"{Inbox}/read-all", new { category = "requests", userId = BobId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await carol.PostAsync($"{Inbox}/no-es-id/read", null)).StatusCode);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Count)).StatusCode);
    }

    [Fact]
    public async Task A_failing_store_does_not_break_the_action_and_the_inbox_fails_closed()
    {
        using var factory = new UserResourceFactory();
        var (alice, roomId) = await ApprovedRoomOwnedByAliceAsync(factory);
        using var _ = alice;
        using var carol = await factory.ClientForAsync(CarolId);
        factory.Notifications.Unavailable = true;

        using var request = await carol.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola" });

        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        Assert.Contains(factory.Logs, line => line.Contains("request_received", StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs, line => line.Contains(CarolId, StringComparison.Ordinal) && line.Contains("Notification", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await carol.GetAsync(Inbox)).StatusCode);

        factory.Notifications.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await carol.GetAsync(Count)).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static string Seed(UserResourceFactory factory, string userId, string type, string category)
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(factory.Notifications.Items.Count);
        var id = (0x6b00_0000_0000L + factory.Notifications.Items.Count).ToString("x24", System.Globalization.CultureInfo.InvariantCulture);
        factory.Notifications.Items.Add(new Notification
        {
            Id = id,
            UserId = userId,
            Type = type,
            Category = category,
            Title = "Aviso",
            Body = "Texto",
            TargetKind = "request",
            TargetId = id,
            DedupeKey = $"{type}:{id}",
            CreatedAt = now,
            UpdatedAt = now,
        });
        return id;
    }

    // Alice publica un edificio y Bob (revisor) lo aprueba: el cuarto queda disponible.
    private static async Task<(HttpClient Alice, string RoomId)> ApprovedRoomOwnedByAliceAsync(UserResourceFactory factory)
    {
        var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        await factory.Moderation.GrantAsync(BobId, StaffRoles.Reviewer, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        (await alice.PostAsJsonAsync("/api/v1/landlord/profile", new { fullName = "Ana Arrendadora", phone = "+527731234567", rentalMode = "individual", acceptedTermsVersion = "2026-11" })).Dispose();
        using var created = await alice.PostAsJsonAsync("/api/v1/landlord/buildings", new
        {
            type = "rooms_building",
            name = "Casa Estudiantes",
            address = new { street = "Calle 1", number = "1", neighborhood = "Centro", city = "Tula de Allende", state = "Hidalgo", postalCode = "42800" },
            location = new { lat = 20.0150, lng = -99.3420 },
            nearestUniversityId = factory.Universities.Universities.Single(university => university.Code == "UTTT").Id,
            allowedTenants = "any",
            services = new[] { "wifi" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var building = factory.Buildings.Buildings.Values.Single();
        (await alice.PostAsJsonAsync($"/api/v1/landlord/buildings/{building.Id}/rooms/generate", new
        {
            floors = 1,
            roomsPerFloor = new[] { 1 },
            @base = new { price = 2800, sizeM2 = 12m, type = "single", bathroom = "shared", availableFrom = "2026-12-01", minContract = "six_months" },
        })).Dispose();
        Assert.Equal(HttpStatusCode.Accepted, (await alice.PostAsync($"/api/v1/landlord/buildings/{building.Id}/publish", null)).StatusCode);
        var version = factory.Buildings.Buildings[building.Id].Version;
        using var approved = await bob.PostAsJsonAsync($"/api/v1/admin/buildings/{building.Id}/approve", new { version });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return (alice, factory.Rooms.Rooms.Values.Single().Id);
    }
}
