using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-20 1.3.0 T09–T11: los 6 avisos nuevos (§9.1) salen de las acciones
// reales, nunca al actor, sin datos de más y con los destinos `rental`,
// `room` y `none`.
public sealed class NotificationsV13HttpTests
{
    private const string Inbox = "/api/v1/users/me/notifications";

    [Fact]
    public async Task Agreement_rental_and_review_notify_the_other_party()
    {
        using var factory = new UserResourceFactory();
        var (building, rooms) = await BuildingOfBobAsync(factory, 1);
        var roomId = rooms[0].Id;
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);
        Assert.True((await alice.PutAsync($"/api/v1/users/me/favorites/{roomId}", null)).IsSuccessStatusCode);
        Assert.True((await carol.PutAsync($"/api/v1/users/me/favorites/{roomId}", null)).IsSuccessStatusCode);
        var requestId = await AcceptedRequestAsync(alice, bob, roomId);

        // T09: convenio por aceptar, al estudiante y con prioridad alta.
        var (agreementId, code) = await CreateAgreementAsync(bob, requestId);
        var proposed = await SingleAsync(alice, NotificationTypes.AgreementProposed);
        Assert.Equal("Convenio por aceptar", proposed.GetProperty("title").GetString());
        Assert.Equal("Bob preparó el convenio de Casa Verde · Cuarto 101. Pídele el código de cierre para aceptarlo.", proposed.GetProperty("body").GetString());
        Assert.Equal("requests", proposed.GetProperty("category").GetString());
        Assert.Equal("rental", proposed.GetProperty("target").GetProperty("kind").GetString());
        Assert.Equal(agreementId, proposed.GetProperty("target").GetProperty("id").GetString());
        Assert.DoesNotContain(code, proposed.GetRawText());
        Assert.Contains(NotificationTypes.AgreementProposed, PushRules.HighPriority);
        Assert.Empty(await TypesAsync(bob, NotificationTypes.AgreementProposed));

        // Rentado: avisa a quien lo tenía en favoritos, salvo al estudiante que lo rentó.
        using (var accepted = await alice.PostAsJsonAsync("/api/v1/agreements/accept", new { code, acceptedTerms = true }))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        var unavailable = await SingleAsync(carol, NotificationTypes.FavoriteUnavailable);
        Assert.Equal("Casa Verde · Cuarto 101 ya no está disponible.", unavailable.GetProperty("body").GetString());
        Assert.Equal("room", unavailable.GetProperty("target").GetProperty("kind").GetString());
        Assert.Equal(roomId, unavailable.GetProperty("target").GetProperty("id").GetString());
        Assert.Empty(await TypesAsync(alice, NotificationTypes.FavoriteUnavailable));
        Assert.Empty(await TypesAsync(bob, NotificationTypes.FavoriteUnavailable));

        // T10: reseña recibida, sin calificación ni texto.
        using var review = await alice.PostAsJsonAsync($"/api/v1/rooms/{roomId}/reviews", new
        {
            landlord = new { communication = 4, attention = 4, compliance = 4, respect = 4 },
            property = new { cleanliness = 4, location = 4, value = 4, condition = 4, services = 4 },
            comment = "Muy limpio y cerca",
        });
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        var reviewId = JsonDocument.Parse(await review.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        var received = await SingleAsync(bob, NotificationTypes.ReviewReceived);
        Assert.Equal("Nueva reseña", received.GetProperty("title").GetString());
        Assert.Equal("Un estudiante dejó una reseña de Casa Verde · Cuarto 101.", received.GetProperty("body").GetString());
        Assert.Equal("system", received.GetProperty("category").GetString());
        Assert.Equal(roomId, received.GetProperty("target").GetProperty("id").GetString());
        Assert.DoesNotContain("Muy limpio", received.GetRawText());
        Assert.Empty(await TypesAsync(alice, NotificationTypes.ReviewReceived));

        // T10: reseña ocultada, sin el motivo; volver a mostrarla no avisa.
        await factory.Moderation.GrantAsync(CarolId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await carol.PostAsJsonAsync($"/api/v1/admin/reviews/{reviewId}/hide", new { reason = "Lenguaje ofensivo" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await carol.PostAsJsonAsync($"/api/v1/admin/reviews/{reviewId}/unhide", new { reason = "Revisado" })).StatusCode);
        var hidden = await SingleAsync(alice, NotificationTypes.ReviewHidden);
        Assert.Equal("Tu reseña de Casa Verde · Cuarto 101 se ocultó porque no cumple las reglas de la comunidad.", hidden.GetProperty("body").GetString());
        Assert.Equal("room", hidden.GetProperty("target").GetProperty("kind").GetString());
        Assert.DoesNotContain("ofensivo", hidden.GetRawText());
        Assert.Empty(await TypesAsync(carol, NotificationTypes.ReviewHidden));
        _ = building;
    }

    [Fact]
    public async Task Cancelled_agreement_notifies_the_student_once()
    {
        using var factory = new UserResourceFactory();
        var (_, rooms) = await BuildingOfBobAsync(factory, 1);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var requestId = await AcceptedRequestAsync(alice, bob, rooms[0].Id);
        var (agreementId, _) = await CreateAgreementAsync(bob, requestId);

        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsync($"/api/v1/landlord/agreements/{agreementId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsync($"/api/v1/landlord/agreements/{agreementId}/cancel", null)).StatusCode);

        var cancelled = await SingleAsync(alice, NotificationTypes.AgreementCancelled);
        Assert.Equal("Convenio cancelado", cancelled.GetProperty("title").GetString());
        Assert.Equal("Bob canceló el convenio de Casa Verde · Cuarto 101.", cancelled.GetProperty("body").GetString());
        Assert.Equal("rental", cancelled.GetProperty("target").GetProperty("kind").GetString());
        Assert.Equal(agreementId, cancelled.GetProperty("target").GetProperty("id").GetString());
        Assert.Empty(await TypesAsync(bob, NotificationTypes.AgreementCancelled));
    }

    [Fact]
    public async Task Favorite_unavailable_on_pause_delete_and_suspension_once_per_day()
    {
        using var factory = new UserResourceFactory();
        var (building, rooms) = await BuildingOfBobAsync(factory, 3);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);
        foreach (var room in rooms)
        {
            Assert.True((await carol.PutAsync($"/api/v1/users/me/favorites/{room.Id}", null)).IsSuccessStatusCode);
        }

        Assert.True((await bob.PutAsync($"/api/v1/users/me/favorites/{rooms[0].Id}", null)).IsSuccessStatusCode);

        // Pausa: un aviso; leerlo, reactivar y volver a pausar el mismo día no repite.
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsync($"/api/v1/landlord/rooms/{rooms[0].Id}/pause", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await carol.PostAsync($"{Inbox}/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsync($"/api/v1/landlord/rooms/{rooms[0].Id}/resume", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsync($"/api/v1/landlord/rooms/{rooms[0].Id}/pause", null)).StatusCode);
        Assert.Single(factory.Notifications.Items, item => item.UserId == CarolId && item.Type == NotificationTypes.FavoriteUnavailable && item.TargetId == rooms[0].Id);
        Assert.Empty(await TypesAsync(bob, NotificationTypes.FavoriteUnavailable));

        // Eliminado por el arrendador.
        var version = factory.Rooms.Rooms[rooms[1].Id].Version;
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/v1/landlord/rooms/{rooms[1].Id}?version={version}")).StatusCode);
        var deleted = Assert.Single(factory.Notifications.Items, item => item.UserId == CarolId && item.Type == NotificationTypes.FavoriteUnavailable && item.TargetId == rooms[1].Id);
        Assert.Equal("El cuarto ya no está disponible.", deleted.Body);

        // Edificio suspendido: solo los cuartos publicados (el pausado ya avisó).
        await factory.Moderation.GrantAsync(AliceId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync($"/api/v1/admin/buildings/{building.Id}/suspend", new { reason = "Contenido dudoso." })).StatusCode);
        Assert.Single(factory.Notifications.Items, item => item.UserId == CarolId && item.Type == NotificationTypes.FavoriteUnavailable && item.TargetId == rooms[2].Id);
        Assert.Equal(3, factory.Notifications.Items.Count(item => item.Type == NotificationTypes.FavoriteUnavailable));
        Assert.All(factory.Notifications.Items.Where(item => item.Type == NotificationTypes.FavoriteUnavailable), item => Assert.Equal(CarolId, item.UserId));
    }

    [Fact]
    public async Task Resolved_report_notifies_the_reporter_without_the_sanction()
    {
        using var factory = new UserResourceFactory();
        var (building, _) = await BuildingOfBobAsync(factory, 1);
        using var alice = await factory.ClientForAsync(AliceId);
        using var carol = await factory.ClientForAsync(CarolId);
        using var report = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = building.Id, reason = "fraud" });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var reportId = JsonDocument.Parse(await report.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;

        await factory.Moderation.GrantAsync(AliceId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        using var resolved = await alice.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/resolve", new { action = "suspend_building", reason = "Fraude confirmado." });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var notice = await SingleAsync(carol, NotificationTypes.ReportResolved);
        Assert.Equal("Reporte revisado", notice.GetProperty("title").GetString());
        Assert.Equal("Revisamos tu reporte. Gracias por ayudarnos a cuidar la comunidad.", notice.GetProperty("body").GetString());
        Assert.Equal("none", notice.GetProperty("target").GetProperty("kind").GetString());
        Assert.Equal(reportId, notice.GetProperty("target").GetProperty("id").GetString());
        var raw = notice.GetRawText();
        Assert.DoesNotContain("Fraude", raw);
        Assert.DoesNotContain("suspend", raw);
        Assert.Empty(await TypesAsync(alice, NotificationTypes.ReportResolved));
    }

    [Fact]
    public async Task Favorites_failure_does_not_break_the_action()
    {
        using var factory = new UserResourceFactory();
        var (_, rooms) = await BuildingOfBobAsync(factory, 1);
        using var bob = await factory.ClientForAsync(BobId);
        factory.Favorites.Unavailable = true;
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsync($"/api/v1/landlord/rooms/{rooms[0].Id}/pause", null)).StatusCode);
        Assert.DoesNotContain(factory.Notifications.Items, item => item.Type == NotificationTypes.FavoriteUnavailable);
    }

    [Fact]
    public void Catalog_has_the_new_types_and_targets()
    {
        Assert.Equal(22, NotificationTypes.Catalog.Count);
        Assert.Equal((NotificationCategories.Requests, NotificationTargets.Rental), NotificationTypes.Catalog[NotificationTypes.AgreementProposed]);
        Assert.Equal((NotificationCategories.Requests, NotificationTargets.Rental), NotificationTypes.Catalog[NotificationTypes.AgreementCancelled]);
        Assert.Equal((NotificationCategories.System, NotificationTargets.Room), NotificationTypes.Catalog[NotificationTypes.ReviewReceived]);
        Assert.Equal((NotificationCategories.System, NotificationTargets.Room), NotificationTypes.Catalog[NotificationTypes.ReviewHidden]);
        Assert.Equal((NotificationCategories.System, NotificationTargets.Room), NotificationTypes.Catalog[NotificationTypes.FavoriteUnavailable]);
        Assert.Equal((NotificationCategories.System, NotificationTargets.None), NotificationTypes.Catalog[NotificationTypes.ReportResolved]);
        Assert.Equal(200, FavoriteNotices.MaximumRecipients);
    }

    private static async Task<(Building Building, IReadOnlyList<Room> Rooms)> BuildingOfBobAsync(UserResourceFactory factory, int count)
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
                Latitude = 20.05,
                Longitude = -99.34,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None);
        var rooms = await factory.Rooms.CreateManyAsync(
            Enumerable.Range(1, count)
                .Select(index => new Room { BuildingId = building.Id, OwnerId = BobId, Number = 100 + index, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 })
                .ToArray(),
            CancellationToken.None);
        return (building, rooms);
    }

    private static async Task<string> AcceptedRequestAsync(HttpClient student, HttpClient landlord, string roomId)
    {
        using var request = await student.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola, me interesa" });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var requestId = JsonDocument.Parse(await request.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await landlord.PostAsync($"/api/v1/landlord/requests/{requestId}/accept", null)).StatusCode);
        return requestId;
    }

    private static async Task<(string Id, string Code)> CreateAgreementAsync(HttpClient landlord, string requestId)
    {
        var start = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, VisitSchedulePolicy.MexicoCity).AddDays(5).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var response = await landlord.PostAsJsonAsync("/api/v1/landlord/agreements", new { requestId, monthlyRent = 3000, deposit = 3000, startDate = start, durationMonths = 6, paymentDay = 5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (document.RootElement.GetProperty("id").GetString()!, document.RootElement.GetProperty("closingCode").GetProperty("backupCode").GetString()!);
    }

    private static async Task<JsonElement[]> TypesAsync(HttpClient client, string type)
    {
        using var response = await client.GetAsync(Inbox + "?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == type)
            .Select(item => item.Clone())
            .ToArray();
    }

    private static async Task<JsonElement> SingleAsync(HttpClient client, string type) => Assert.Single(await TypesAsync(client, type));
}
