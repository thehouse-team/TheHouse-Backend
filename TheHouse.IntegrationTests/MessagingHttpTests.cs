using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-16 T01–T09: contrato HTTP de solicitudes y chat, BOLA, códigos de
// conflicto, consulta incremental, rate limit, fallo cerrado y privacidad.
public sealed class MessagingHttpTests
{
    private const string Mine = "/api/v1/users/me/requests";
    private const string Landlord = "/api/v1/landlord/requests";
    private const string Conversations = "/api/v1/conversations";
    private const string Phone = "+527711234567";

    [Fact]
    public async Task Student_requests_landlord_accepts_and_both_chat()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        using var created = await CreateAsync(alice, roomId, new { quickQuestions = new[] { "deposit", "visit_possible" }, message = "Hola, ¿sigue disponible?" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
        using var request = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var root = request.RootElement;
        Assert.Equal(
            new[] { "id", "status", "room", "quickQuestions", "message", "student", "landlord", "visit", "conversationId", "rejection", "roomAvailable", "createdAt", "updatedAt" },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "id", "buildingId", "title", "city" }, root.GetProperty("room").EnumerateObject().Select(property => property.Name));
        Assert.Equal("pending", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("student").ValueKind);
        Assert.Equal("Bob", root.GetProperty("landlord").GetProperty("name").GetString());
        var id = root.GetProperty("id").GetString()!;
        var conversationId = root.GetProperty("conversationId").GetString()!;

        using var inbox = await bob.GetAsync($"{Landlord}?status=pending");
        using (var document = JsonDocument.Parse(await inbox.Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(id, item.GetProperty("id").GetString());
            Assert.Equal(new[] { "name", "university" }, item.GetProperty("student").EnumerateObject().Select(property => property.Name));
        }

        using var accepted = await bob.PostAsync($"{Landlord}/{id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("accepted", await StatusAsync(alice, $"{Mine}/{id}"));

        var clientId = Guid.NewGuid().ToString();
        using var sent = await SendAsync(alice, conversationId, "Gracias, ¿cuándo puedo verlo?", clientId);
        using var repeated = await SendAsync(alice, conversationId, "Gracias, ¿cuándo puedo verlo?", clientId);
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var messageId = await IdAsync(sent);
        Assert.Equal(messageId, await IdAsync(repeated));
        Assert.Single(factory.Conversations.Messages);

        using var conversations = await bob.GetAsync(Conversations);
        using (var document = JsonDocument.Parse(await conversations.Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(
                new[] { "id", "requestId", "room", "counterpart", "status", "lastMessageAt", "unreadCount" },
                item.EnumerateObject().Select(property => property.Name));
            Assert.Equal(1, item.GetProperty("unreadCount").GetInt32());
            Assert.Equal("student", item.GetProperty("counterpart").GetProperty("role").GetString());
        }

        using var messages = await bob.GetAsync($"{Conversations}/{conversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, messages.StatusCode);
        var etag = messages.Headers.ETag;
        Assert.NotNull(etag);
        using (var document = JsonDocument.Parse(await messages.Content.ReadAsStringAsync()))
        {
            Assert.Equal(new[] { "items", "hasMore", "pollAfterSeconds" }, document.RootElement.EnumerateObject().Select(property => property.Name));
            var message = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(new[] { "id", "senderRole", "text", "createdAt", "mine" }, message.EnumerateObject().Select(property => property.Name));
            Assert.False(message.GetProperty("mine").GetBoolean());
            Assert.Equal(10, document.RootElement.GetProperty("pollAfterSeconds").GetInt32());
        }

        using var poll = new HttpRequestMessage(HttpMethod.Get, $"{Conversations}/{conversationId}/messages?after={messageId}");
        poll.Headers.IfNoneMatch.Add(etag!);
        using var notModified = await bob.SendAsync(poll);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

        using var read = await bob.PostAsJsonAsync($"{Conversations}/{conversationId}/read", new { lastMessageId = messageId });
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        using var afterRead = await bob.GetAsync(Conversations);
        using (var document = JsonDocument.Parse(await afterRead.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, document.RootElement.GetProperty("items")[0].GetProperty("unreadCount").GetInt32());
        }
    }

    [Theory]
    [InlineData("{\"quickQuestions\":[\"deposit\"],\"status\":\"accepted\"}")]
    [InlineData("{\"quickQuestions\":[\"deposit\"],\"studentId\":\"665f1c2a9b1e4a0000000b0b\"}")]
    [InlineData("{\"quickQuestions\":[\"price\"]}")]
    [InlineData("{\"message\":\"   \"}")]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task Create_rejects_bad_input_with_400(string body)
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PostAsync($"/api/v1/rooms/{roomId}/requests", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.RentalRequests.Requests);
    }

    [Fact]
    public async Task Unpublished_or_unknown_rooms_and_foreign_resources_are_404()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var carol = await factory.ClientForAsync(CarolId);
        using var created = await CreateAsync(alice, roomId, new { message = "Hola" });
        var (id, conversationId) = await IdsAsync(created);
        await MakeLandlordAsync(factory, CarolId, "Carolina Díaz");

        using var unknown = await CreateAsync(alice, "ffffffffffffffffffffffff", new { message = "Hola" });
        using var foreignMine = await carol.GetAsync($"{Mine}/{id}");
        using var foreignReceived = await carol.GetAsync($"{Landlord}/{id}");
        using var foreignAccept = await carol.PostAsync($"{Landlord}/{id}/accept", null);
        using var foreignMessages = await carol.GetAsync($"{Conversations}/{conversationId}/messages");
        using var foreignSend = await SendAsync(carol, conversationId, "hola", Guid.NewGuid().ToString());
        using var studentAccept = await alice.PostAsync($"{Landlord}/{id}/accept", null);
        using var badId = await alice.GetAsync($"{Mine}/no-es-id");

        var room = factory.Rooms.Rooms[roomId];
        factory.Rooms.Rooms[roomId] = room with { Status = RoomStatus.Paused };
        using var paused = await CreateAsync(carol, roomId, new { message = "Hola" });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignMine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignReceived.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignAccept.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignMessages.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignSend.StatusCode);
        await AssertProblemAsync(studentAccept, HttpStatusCode.Forbidden, "landlord_required");
        Assert.Equal(HttpStatusCode.BadRequest, badId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, paused.StatusCode);
        Assert.Equal("pending", factory.RentalRequests.Requests[id].Status.ToString().ToLowerInvariant());
        Assert.Empty(factory.Conversations.Messages);
    }

    [Fact]
    public async Task Conflicts_and_rules_return_their_codes()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var created = await CreateAsync(alice, roomId, new { quickQuestions = new[] { "deposit" } });
        var (id, conversationId) = await IdsAsync(created);

        using var duplicate = await CreateAsync(alice, roomId, new { message = "Otra vez" });
        using var own = await CreateAsync(bob, roomId, new { message = "Mío" });
        using var otherReason = await bob.PostAsJsonAsync($"{Landlord}/{id}/reject", new { reason = "other" });
        using var rejected = await bob.PostAsJsonAsync($"{Landlord}/{id}/reject", new { reason = "profile_mismatch", note = "Busco estudiantes de posgrado" });
        using var acceptRejected = await bob.PostAsync($"{Landlord}/{id}/accept", null);
        using var cancelRejected = await alice.PostAsync($"{Mine}/{id}/cancel", null);
        using var closed = await SendAsync(alice, conversationId, "¿Por qué?", Guid.NewGuid().ToString());

        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "active_request_exists");
        await AssertProblemAsync(own, (HttpStatusCode)422, "own_room");
        Assert.Equal(HttpStatusCode.BadRequest, otherReason.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        await AssertProblemAsync(acceptRejected, HttpStatusCode.Conflict, "request_state");
        await AssertProblemAsync(cancelRejected, HttpStatusCode.Conflict, "request_state");
        await AssertProblemAsync(closed, HttpStatusCode.Conflict, "conversation_closed");
        Assert.Equal("rejected", await StatusAsync(alice, $"{Mine}/{id}"));
    }

    [Fact]
    public async Task Suspended_landlord_blocks_new_requests_and_messages()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var created = await CreateAsync(alice, roomId, new { message = "Hola" });
        var (_, conversationId) = await IdsAsync(created);
        var secondRoom = await PublishRoomForBobAsync(factory, number: 102);

        await factory.Moderation.CreateAsync(new UserSuspension(BobId, "fraude", "admin", DateTimeOffset.UtcNow), CancellationToken.None);
        using var request = await CreateAsync(alice, secondRoom, new { message = "Hola" });
        using var message = await SendAsync(alice, conversationId, "¿Hola?", Guid.NewGuid().ToString());

        await AssertProblemAsync(request, HttpStatusCode.Conflict, "participant_suspended");
        await AssertProblemAsync(message, HttpStatusCode.Conflict, "participant_suspended");
    }

    [Fact]
    public async Task Student_cancels_and_conversation_closes()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var created = await CreateAsync(alice, roomId, new { message = "Hola" });
        var (id, conversationId) = await IdsAsync(created);

        using var cancelled = await alice.PostAsync($"{Mine}/{id}/cancel", null);
        using var list = await alice.GetAsync($"{Mine}?status=cancelled");
        using var conversations = await alice.GetAsync(Conversations);
        using var badStatus = await alice.GetAsync($"{Mine}?status=closed");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            Assert.Equal(id, Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray()).GetProperty("id").GetString());
        }

        using (var document = JsonDocument.Parse(await conversations.Content.ReadAsStringAsync()))
        {
            var item = document.RootElement.GetProperty("items")[0];
            Assert.Equal(conversationId, item.GetProperty("id").GetString());
            Assert.Equal("closed", item.GetProperty("status").GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task Rate_limits_return_429_with_retry_after()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { RequestCreatesPerDay = 1, MessagePollsPerWindow = 1 });
        var roomId = await PublishRoomForBobAsync(factory);
        var secondRoom = await PublishRoomForBobAsync(factory, number: 102);
        using var alice = await factory.ClientForAsync(AliceId);
        using var created = await CreateAsync(alice, roomId, new { message = "Hola" });
        var (_, conversationId) = await IdsAsync(created);

        using var limitedCreate = await CreateAsync(alice, secondRoom, new { message = "Hola" });
        (await alice.GetAsync($"{Conversations}/{conversationId}/messages")).Dispose();
        using var limitedPoll = await alice.GetAsync($"{Conversations}/{conversationId}/messages");

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedCreate.StatusCode);
        Assert.True(limitedCreate.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedPoll.StatusCode);
        Assert.Equal("60", limitedPoll.Headers.GetValues("Retry-After").Single());
        Assert.Single(factory.RentalRequests.Requests);
    }

    [Fact]
    public async Task Fails_closed_when_mongo_or_redis_are_down()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);

        factory.RentalRequests.Unavailable = true;
        using var mongoDown = await CreateAsync(alice, roomId, new { message = "Hola" });
        factory.RentalRequests.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        using var redisDown = await alice.GetAsync(Conversations);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);
    }

    [Fact]
    public async Task Requires_a_valid_bearer_token()
    {
        using var factory = new UserResourceFactory();
        using var anonymous = factory.CreateHttpsClient();

        using var request = await anonymous.PostAsJsonAsync("/api/v1/rooms/ffffffffffffffffffffffff/requests", new { message = "Hola" });
        using var conversations = await anonymous.GetAsync(Conversations);

        Assert.Equal(HttpStatusCode.Unauthorized, request.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, conversations.StatusCode);
    }

    [Fact]
    public async Task No_contact_data_in_responses_and_no_content_in_logs()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var created = await CreateAsync(alice, roomId, new { message = "texto-privado-solicitud" });
        var (id, conversationId) = await IdsAsync(created);
        using var sent = await SendAsync(alice, conversationId, "texto-privado-chat", Guid.NewGuid().ToString());

        var bodies = new List<string>();
        foreach (var (client, path) in new[] { (bob, $"{Landlord}/{id}"), (alice, $"{Mine}/{id}"), (bob, Conversations), (bob, $"{Conversations}/{conversationId}/messages") })
        {
            using var response = await client.GetAsync(path);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        foreach (var body in bodies)
        {
            Assert.DoesNotContain(Phone, body, StringComparison.Ordinal);
            Assert.DoesNotContain("@example.com", body, StringComparison.Ordinal);
            Assert.DoesNotContain(BobId, body, StringComparison.Ordinal);
            Assert.DoesNotContain(AliceId, body, StringComparison.Ordinal);
            Assert.DoesNotContain("Pérez", body, StringComparison.Ordinal);
        }

        Assert.NotEmpty(factory.Logs);
        Assert.DoesNotContain(factory.Logs, line => line.Contains("texto-privado", StringComparison.Ordinal));
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

    // Bob es arrendador con un edificio verificado y un cuarto disponible.
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
                Latitude = 20.05,
                Longitude = -99.34,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        var rooms = await factory.Rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = BobId, Number = number, Floor = 1, Price = 2800, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None);
        return rooms[0].Id;
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string roomId, object body) =>
        client.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", body);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string conversationId, string text, string clientMessageId) =>
        client.PostAsJsonAsync($"{Conversations}/{conversationId}/messages", new { text, clientMessageId });

    private static async Task<string> IdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<(string Id, string ConversationId)> IdsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (document.RootElement.GetProperty("id").GetString()!, document.RootElement.GetProperty("conversationId").GetString()!);
    }

    private static async Task<string> StatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("status").GetString()!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
    }
}
