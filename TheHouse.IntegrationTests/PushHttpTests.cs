using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-20 1.2.0 T01–T04: dispositivos por HTTP y push real a través de la cola
// después de guardar la notificación (emisor falso de FCM).
public sealed class PushHttpTests
{
    private const string Devices = "/api/v1/users/me/devices";

    [Fact]
    public async Task Devices_are_registered_listed_and_deleted_without_exposing_tokens()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        var installation = Guid.NewGuid().ToString();

        using (var created = await alice.PutAsJsonAsync($"{Devices}/{installation}", Body("alice-phone")))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
            var raw = await created.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(raw);
            Assert.Equal(["installationId", "platform", "appVersion", "registeredAt", "updatedAt"], document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain(Token("alice-phone"), raw, StringComparison.Ordinal);
        }

        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync($"{Devices}/{installation}", Body("alice-phone-2"))).StatusCode);
        var list = await (await alice.GetAsync(Devices)).Content.ReadAsStringAsync();
        Assert.Contains(installation, list, StringComparison.Ordinal);
        Assert.DoesNotContain("fcm-", list, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", new { fcmToken = Token("x"), platform = "ios" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", new { fcmToken = Token("x"), platform = "android", userId = BobId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PutAsJsonAsync($"{Devices}/no-es-uuid", Body("x"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"{Devices}/{installation}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DeleteAsync($"{Devices}/{installation}")).StatusCode);
        Assert.Empty(factory.PushDevices.Items);

        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Devices)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", Body("x"))).StatusCode);
    }

    [Fact]
    public async Task Other_accounts_cannot_touch_my_installation()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var installation = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Created, (await alice.PutAsJsonAsync($"{Devices}/{installation}", Body("alice-phone"))).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"{Devices}/{installation}")).StatusCode);
        using (var conflict = await bob.PutAsJsonAsync($"{Devices}/{installation}", Body("bob-phone")))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            using var document = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
            Assert.Equal("installation_conflict", document.RootElement.GetProperty("code").GetString());
        }

        Assert.Equal(AliceId, Assert.Single(factory.PushDevices.Items).UserId);
    }

    [Fact]
    public async Task Notifications_are_pushed_to_the_recipient_and_never_to_the_actor()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        Assert.Equal(HttpStatusCode.Created, (await bob.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", Body("bob-phone"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await alice.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", Body("alice-phone"))).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Mensaje privado de prueba" })).StatusCode);

        var (token, message) = await WaitForPushAsync(factory, Token("bob-phone"));
        Assert.Equal(Token("bob-phone"), token);
        Assert.Equal(NotificationTypes.RequestReceived, message.Type);
        Assert.Equal("Nueva solicitud", message.Title);
        Assert.DoesNotContain("Mensaje privado", message.Body, StringComparison.Ordinal);
        var stored = Assert.Single(factory.Notifications.Items, item => item.UserId == BobId);
        Assert.Equal(stored.Id, message.NotificationId);
        Assert.Equal(stored.TargetId, message.TargetId);
        Assert.DoesNotContain(factory.PushSender.Sent, item => item.Token == Token("alice-phone"));
    }

    [Fact]
    public async Task Invalid_tokens_are_removed_and_deleting_the_account_removes_devices()
    {
        using var factory = new UserResourceFactory();
        var roomId = await PublishRoomForBobAsync(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        Assert.Equal(HttpStatusCode.Created, (await bob.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", Body("bob-old"))).StatusCode);
        factory.PushSender.Invalid[Token("bob-old")] = true;

        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola" })).StatusCode);
        await WaitUntilAsync(() => factory.PushDevices.Items.All(item => item.UserId != BobId));

        Assert.Equal(HttpStatusCode.Created, (await alice.PutAsJsonAsync($"{Devices}/{Guid.NewGuid()}", Body("alice-phone"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/api/v1/users/me/deletion", new { password = Password, confirmation = "ELIMINAR" })).StatusCode);
        Assert.Empty(factory.PushDevices.Items);
    }

    // --- Apoyo -------------------------------------------------------

    private static string Token(string seed) => "fcm-" + seed.PadRight(24, 'x') + ":APA91b";

    private static object Body(string seed) => new { fcmToken = Token(seed), platform = "android", appVersion = "1.0.0", locale = "es-MX" };

    private static async Task<(string Token, TheHouse.Application.Abstractions.Notifications.PushMessage Message)> WaitForPushAsync(UserResourceFactory factory, string token)
    {
        await WaitUntilAsync(() => factory.PushSender.Sent.Any(item => item.Token == token));
        return factory.PushSender.Sent.First(item => item.Token == token);
    }

    // La cola corre fuera de la respuesta HTTP: se espera hasta 15 s (la suite
    // completa corre en paralelo y el worker puede tardar en tomar el trabajo).
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 300 && !condition(); attempt++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }

    private static async Task<string> PublishRoomForBobAsync(UserResourceFactory factory)
    {
        await factory.Landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = BobId,
                FullName = "Bob Pérez",
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
                OwnerId = BobId,
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
            [new Room { BuildingId = building.Id, OwnerId = BobId, Number = 101, Floor = 1, Price = 2800, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None);
        return rooms[0].Id;
    }
}
