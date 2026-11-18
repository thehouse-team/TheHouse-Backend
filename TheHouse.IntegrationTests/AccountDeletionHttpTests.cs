using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-10 1.2.0 T01–T05: vista previa, eliminación módulo por módulo, sesiones
// cerradas, bloqueos, reintento tras una falla y aviso legal por HTTP.
public sealed class AccountDeletionHttpTests
{
    private const string Deletion = "/api/v1/users/me/deletion";

    [Fact]
    public async Task Student_deletion_erases_cancels_anonymizes_and_closes_sessions()
    {
        using var factory = new UserResourceFactory();
        var (_, roomId) = await PublishRoomAsync(factory, BobId);
        using var bob = await factory.ClientForAsync(BobId);
        var (client, refreshToken) = await LoginAsync(factory, AliceId);
        using var alice = client;
        factory.Profiles.Profiles[AliceId] = new UserProfile { UserId = AliceId, Name = "Alice Gómez", UniversityId = "u1", UpdatedAt = DateTimeOffset.UtcNow };

        Assert.True((await alice.PutAsync($"/api/v1/users/me/favorites/{roomId}", null)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola, ¿sigue disponible?" })).StatusCode);
        using (var visit = new HttpRequestMessage(HttpMethod.Post, "/api/v1/visits") { Content = JsonContent.Create(new { roomId, scheduledAt = Slot(3, 11) }) })
        {
            visit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Created, (await alice.SendAsync(visit)).StatusCode);
        }

        // Vista previa: lo que se borra o cancela, sin bloqueos.
        using (var preview = await alice.GetAsync(Deletion))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Contains("no-store", preview.Headers.CacheControl?.ToString());
            using var document = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.Equal(["canDelete", "blockers", "effects"], root.EnumerateObject().Select(property => property.Name));
            Assert.True(root.GetProperty("canDelete").GetBoolean());
            var effects = root.GetProperty("effects");
            Assert.Equal(
                ["buildings", "rooms", "upcomingVisits", "openRequests", "pendingAgreements", "conversations", "reviews", "favorites", "roommateProfile"],
                effects.EnumerateObject().Select(property => property.Name));
            Assert.Equal(1, effects.GetProperty("favorites").GetInt32());
            Assert.Equal(1, effects.GetProperty("openRequests").GetInt32());
            Assert.Equal(1, effects.GetProperty("upcomingVisits").GetInt32());
            Assert.Equal(1, effects.GetProperty("conversations").GetInt32());
        }

        // Confirmación y contraseña.
        await AssertCodeAsync(await alice.PostAsJsonAsync(Deletion, new { password = "otra-clave-incorrecta", confirmation = "ELIMINAR" }), HttpStatusCode.Forbidden, "password_mismatch");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "eliminar" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR", userId = BobId })).StatusCode);
        Assert.NotNull(await factory.Users.GetByIdAsync(AliceId, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" })).StatusCode);

        // Datos propios: borrados o anonimizados.
        Assert.Null(await factory.Users.GetByIdAsync(AliceId, CancellationToken.None));
        Assert.Contains(AliceId, factory.Users.Deleted);
        Assert.Empty(await factory.Favorites.ListAllAsync(AliceId, CancellationToken.None));
        Assert.Equal("Usuario eliminado", factory.Profiles.Profiles[AliceId].Name);
        Assert.Null(factory.Profiles.Profiles[AliceId].UniversityId);
        Assert.DoesNotContain(factory.Notifications.Items, item => item.UserId == AliceId);

        // Lo compartido: cancelado y cerrado, con aviso al arrendador.
        Assert.All(factory.RentalRequests.Requests.Values, request => Assert.Equal(RentalRequestStatus.Cancelled, request.Status));
        Assert.All(factory.Conversations.Conversations.Values, conversation => Assert.Equal(ConversationStatus.Closed, conversation.Status));
        Assert.All(factory.Visits.Visits.Values, visit => Assert.Equal(VisitStatus.Cancelled, visit.Status));
        Assert.Contains(factory.Notifications.Items, item => item.UserId == BobId && item.Type == NotificationTypes.RequestCancelled);
        Assert.Contains(factory.Notifications.Items, item => item.UserId == BobId && item.Type == NotificationTypes.VisitCancelled);
        var inbox = await (await bob.GetAsync("/api/v1/conversations")).Content.ReadAsStringAsync();
        Assert.Contains("Usuario eliminado", inbox, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice", inbox, StringComparison.Ordinal);

        // Sesiones cerradas: access token, refresh y login.
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/v1/users/me")).StatusCode);
        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = "alice@example.com", password = Password })).StatusCode);

        // La otra cuenta sigue intacta.
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task Landlord_deletion_retires_buildings_and_cancels_visits_of_students()
    {
        using var factory = new UserResourceFactory();
        var (buildingId, roomId) = await PublishRoomAsync(factory, BobId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using (var visit = new HttpRequestMessage(HttpMethod.Post, "/api/v1/visits") { Content = JsonContent.Create(new { roomId, scheduledAt = Slot(4, 12) }) })
        {
            visit.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Created, (await alice.SendAsync(visit)).StatusCode);
        }

        factory.RoomViews.Seed((await factory.Rooms.GetAsync(roomId, CancellationToken.None))!, "2026-11-10", 3);
        using (var preview = await bob.GetAsync(Deletion))
        {
            using var document = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
            var effects = document.RootElement.GetProperty("effects");
            Assert.Equal(1, effects.GetProperty("buildings").GetInt32());
            Assert.Equal(1, effects.GetProperty("rooms").GetInt32());
            Assert.Equal(1, effects.GetProperty("upcomingVisits").GetInt32());
        }

        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" })).StatusCode);

        Assert.Equal(BuildingStatus.Deleted, factory.Buildings.Buildings[buildingId].Status);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/v1/rooms/{roomId}")).StatusCode);
        var landlord = await factory.Landlords.GetAsync(BobId, CancellationToken.None);
        Assert.Equal("Usuario eliminado", landlord!.FullName);
        Assert.Equal(string.Empty, landlord.Phone);
        Assert.Empty(factory.RoomViews.Items);
        var cancelled = Assert.Single(factory.Visits.Visits.Values);
        Assert.Equal(VisitStatus.Cancelled, cancelled.Status);
        Assert.Contains(factory.Notifications.Items, item => item.UserId == AliceId && item.Type == NotificationTypes.VisitCancelled);
    }

    [Fact]
    public async Task Active_rental_or_staff_role_block_deletion()
    {
        using var factory = new UserResourceFactory();
        var (buildingId, roomId) = await PublishRoomAsync(factory, BobId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var carol = await factory.ClientForAsync(CarolId);
        await factory.Agreements.CreateAsync(
            new Agreement
            {
                Id = "665f1c2a9b1e4a00000a9001",
                RequestId = "665f1c2a9b1e4a00000a9002",
                RoomId = roomId,
                BuildingId = buildingId,
                OwnerId = BobId,
                StudentId = AliceId,
                ConversationId = "665f1c2a9b1e4a00000a9003",
                Status = AgreementStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ActivatedAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        using (var preview = await alice.GetAsync(Deletion))
        {
            using var document = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
            Assert.False(document.RootElement.GetProperty("canDelete").GetBoolean());
            var blocker = Assert.Single(document.RootElement.GetProperty("blockers").EnumerateArray().ToArray());
            Assert.Equal("active_rental", blocker.GetProperty("code").GetString());
        }

        using (var blocked = await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            using var document = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
            Assert.Equal("deletion_blocked", document.RootElement.GetProperty("code").GetString());
            Assert.Equal("active_rental", document.RootElement.GetProperty("blockers")[0].GetProperty("code").GetString());
        }

        Assert.NotNull(await factory.Users.GetByIdAsync(AliceId, CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/api/v1/users/me")).StatusCode);

        await factory.Moderation.GrantAsync(CarolId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        await AssertCodeAsync(await carol.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" }), HttpStatusCode.Conflict, "deletion_blocked");
    }

    [Fact]
    public async Task Failure_midway_keeps_the_session_and_retry_completes()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        factory.Notifications.Unavailable = true;
        await AssertCodeAsync(await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" }), HttpStatusCode.ServiceUnavailable, "deletion_incomplete");
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/api/v1/users/me")).StatusCode);

        factory.Notifications.Unavailable = false;
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact]
    public async Task Deletion_attempts_are_limited_and_anonymous_is_rejected()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync(Deletion, new { password = "incorrecta-" + attempt, confirmation = "ELIMINAR" })).StatusCode);
        }

        using var limited = await alice.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.NotNull(await factory.Users.GetByIdAsync(AliceId, CancellationToken.None));

        using var anonymous = factory.CreateHttpsClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Deletion)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Deletion, new { password = Password, confirmation = "ELIMINAR" })).StatusCode);
    }

    [Fact]
    public async Task Legal_versions_are_public_and_acceptance_is_recorded()
    {
        using var factory = new UserResourceFactory();
        using var anonymous = factory.CreateHttpsClient();
        using (var legal = await anonymous.GetAsync("/api/v1/legal"))
        {
            Assert.Equal(HttpStatusCode.OK, legal.StatusCode);
            Assert.Contains("public", legal.Headers.CacheControl?.ToString());
            using var document = JsonDocument.Parse(await legal.Content.ReadAsStringAsync());
            Assert.Equal(
                ["termsVersion", "privacyVersion", "landlordTermsVersion", "termsUrl", "privacyUrl", "accountDeletionUrl", "updatedAt"],
                document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(LegalOptions.DefaultVersion, document.RootElement.GetProperty("termsVersion").GetString());
        }

        using var alice = await factory.ClientForAsync(AliceId);
        await AssertCodeAsync(await alice.PutAsJsonAsync("/api/v1/users/me/legal", new { termsVersion = "2025-01", privacyVersion = LegalOptions.DefaultVersion }), HttpStatusCode.Conflict, "legal_version_outdated");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync("/api/v1/users/me/legal", new { termsVersion = LegalOptions.DefaultVersion })).StatusCode);

        using (var accepted = await alice.PutAsJsonAsync("/api/v1/users/me/legal", new { termsVersion = LegalOptions.DefaultVersion, privacyVersion = LegalOptions.DefaultVersion }))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var document = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
            Assert.Equal(["termsVersion", "privacyVersion", "acceptedAt"], document.RootElement.EnumerateObject().Select(property => property.Name));
        }

        using var me = JsonDocument.Parse(await (await alice.GetAsync("/api/v1/users/me")).Content.ReadAsStringAsync());
        Assert.True(me.RootElement.GetProperty("legalUpToDate").GetBoolean());
        Assert.Equal(LegalOptions.DefaultVersion, me.RootElement.GetProperty("legal").GetProperty("privacyVersion").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/users/me/legal", new { termsVersion = "x", privacyVersion = "y" })).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static async Task<(HttpClient Client, string RefreshToken)> LoginAsync(UserResourceFactory factory, string userId)
    {
        using (await factory.ClientForAsync(userId))
        {
        }

        var client = factory.CreateHttpsClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = userId == AliceId ? "alice@example.com" : "bob@example.com", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", document.RootElement.GetProperty("accessToken").GetString());
        return (client, document.RootElement.GetProperty("refreshToken").GetString()!);
    }

    private static async Task<(string BuildingId, string RoomId)> PublishRoomAsync(UserResourceFactory factory, string ownerId)
    {
        await factory.Landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = ownerId,
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
