using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-21 T01–T12: roles, revisión de edificios, verificación del dueño,
// reportes, suspensiones y bitácora, con dueño, revisor, admin y usuario
// sin rol.
public sealed class AdminHttpTests
{
    private const string Admin = "/api/v1/admin";
    private const string Landlord = "/api/v1/landlord";

    [Fact]
    public async Task Admin_routes_require_the_right_role()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Reviewer);

        using var noRole = await carol.GetAsync($"{Admin}/buildings");
        using var reviewerQueue = await bob.GetAsync($"{Admin}/buildings");
        using var reviewerReports = await bob.GetAsync($"{Admin}/reports");
        using var reviewerAudit = await bob.GetAsync($"{Admin}/audit");

        await AssertCodeAsync(noRole, HttpStatusCode.Forbidden, "role_required");
        Assert.Equal(HttpStatusCode.OK, reviewerQueue.StatusCode);
        Assert.Contains("no-store", reviewerQueue.Headers.CacheControl?.ToString());
        await AssertCodeAsync(reviewerReports, HttpStatusCode.Forbidden, "role_required");
        await AssertCodeAsync(reviewerAudit, HttpStatusCode.Forbidden, "role_required");

        await Grant(factory, BobId, StaffRoles.Admin);
        using var adminReports = await bob.GetAsync($"{Admin}/reports");
        Assert.Equal(HttpStatusCode.OK, adminReports.StatusCode);
    }

    [Fact]
    public async Task Approving_a_building_verifies_it_and_makes_rooms_available()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Reviewer);
        var building = factory.Buildings.Buildings.Values.Single();

        using var queue = await bob.GetAsync($"{Admin}/buildings?status=in_review");
        using (var document = JsonDocument.Parse(await queue.Content.ReadAsStringAsync()))
        {
            Assert.Equal(building.Id, Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray()).GetProperty("id").GetString());
        }

        using var detail = await bob.GetAsync($"{Admin}/buildings/{building.Id}");
        using (var document = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()))
        {
            Assert.Equal(new[] { "building", "rooms", "landlord", "lastDecision" }, document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(new[] { "fullName", "rentalMode", "rfc" }, document.RootElement.GetProperty("landlord").EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain("+527731234567", document.RootElement.GetRawText());
        }

        using var approved = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version });
        using var again = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version + 1 });
        using var verification = await alice.GetAsync($"{Landlord}/buildings/{building.Id}/verification");

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(BuildingStatus.Verified, factory.Buildings.Buildings[building.Id].Status);
        Assert.All(factory.Rooms.Rooms.Values, room => Assert.Equal(RoomStatus.Available, room.Status));
        await AssertCodeAsync(again, HttpStatusCode.Conflict, "building_state");
        using (var document = JsonDocument.Parse(await verification.Content.ReadAsStringAsync()))
        {
            Assert.Equal("verified", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("approved", document.RootElement.GetProperty("lastDecision").GetProperty("decision").GetString());
            Assert.DoesNotContain(BobId, document.RootElement.GetRawText());
        }

        Assert.Contains(factory.Moderation.Audit, item => item.Action == "building.approved" && item.ActorId == BobId);
    }

    [Fact]
    public async Task Request_changes_and_reject_require_a_reason_and_move_states()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Reviewer);
        var building = factory.Buildings.Buildings.Values.Single();

        using var noReason = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/request-changes", new { version = building.Version });
        using var stale = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/request-changes", new { reason = "Falta la foto de la fachada.", version = 1 });
        using var changes = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/request-changes", new { reason = "Falta la foto de la fachada.", version = building.Version });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        await AssertCodeAsync(stale, HttpStatusCode.Conflict, "version_conflict");
        Assert.Equal(HttpStatusCode.OK, changes.StatusCode);
        Assert.Equal(BuildingStatus.Draft, factory.Buildings.Buildings[building.Id].Status);
        Assert.All(factory.Rooms.Rooms.Values, room => Assert.Equal(RoomStatus.Ready, room.Status));

        using (var verification = await alice.GetAsync($"{Landlord}/buildings/{building.Id}/verification"))
        using (var document = JsonDocument.Parse(await verification.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Falta la foto de la fachada.", document.RootElement.GetProperty("lastDecision").GetProperty("reason").GetString());
        }

        (await alice.PostAsync($"{Landlord}/buildings/{building.Id}/publish", null)).Dispose();
        var current = factory.Buildings.Buildings[building.Id];
        using var rejected = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/reject", new { reason = "Documentos falsos.", version = current.Version });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(BuildingStatus.Rejected, factory.Buildings.Buildings[building.Id].Status);

        var rejectedVersion = factory.Buildings.Buildings[building.Id].Version;
        using var edit = await alice.PatchAsync($"{Landlord}/buildings/{building.Id}", JsonContent.Create(new { name = "Otro", version = rejectedVersion }));
        using var delete = await alice.DeleteAsync($"{Landlord}/buildings/{building.Id}?version={rejectedVersion}");
        await AssertCodeAsync(edit, HttpStatusCode.Conflict, "building_state");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Reviewers_cannot_decide_on_their_own_buildings()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        await Grant(factory, AliceId, StaffRoles.Admin);
        var building = factory.Buildings.Buildings.Values.Single();

        using var own = await alice.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version });

        await AssertCodeAsync(own, HttpStatusCode.Forbidden, "own_building");
        Assert.Equal(BuildingStatus.InReview, factory.Buildings.Buildings[building.Id].Status);
    }

    [Fact]
    public async Task Reports_are_validated_deduplicated_and_resolved()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var carol = await factory.ClientForAsync(CarolId);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Admin);
        var building = factory.Buildings.Buildings.Values.Single();

        using var created = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = building.Id, reason = "fraud", details = "detalle-secreto-123" });
        using var repeated = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = building.Id, reason = "fake" });
        using var missing = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = "ffffffffffffffffffffffff", reason = "fake" });
        using var self = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "user", targetId = CarolId, reason = "spam" });
        using var badReason = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "user", targetId = AliceId, reason = "boring" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await AssertCodeAsync(repeated, HttpStatusCode.Conflict, "report_exists");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);

        using var list = await bob.GetAsync($"{Admin}/reports?status=open");
        string reportId;
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            reportId = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray()).GetProperty("id").GetString()!;
        }

        using var dismissed = await bob.PostAsJsonAsync($"{Admin}/reports/{reportId}/resolve", new { action = "dismiss", reason = "No hay evidencia." });
        using var twice = await bob.PostAsJsonAsync($"{Admin}/reports/{reportId}/resolve", new { action = "dismiss", reason = "No hay evidencia." });
        Assert.Equal(HttpStatusCode.OK, dismissed.StatusCode);
        await AssertCodeAsync(twice, HttpStatusCode.Conflict, "report_state");
        Assert.Equal("dismissed", factory.Moderation.Reports[reportId].Status);
        Assert.DoesNotContain(factory.Logs, line => line.Contains("detalle-secreto-123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Suspending_a_user_blocks_login_refresh_and_current_sessions()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        using var carol = await factory.ClientForAsync(CarolId);
        await Grant(factory, BobId, StaffRoles.Admin);
        var building = factory.Buildings.Buildings.Values.Single();
        (await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version })).Dispose();

        using var report = await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = building.Id, reason = "fraud" });
        var reportId = factory.Moderation.Reports.Keys.Single();
        using var resolved = await bob.PostAsJsonAsync($"{Admin}/reports/{reportId}/resolve", new { action = "suspend_user", reason = "Fraude confirmado." });

        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        Assert.True(factory.Moderation.Suspensions.ContainsKey(AliceId));
        Assert.Equal(BuildingStatus.Suspended, factory.Buildings.Buildings[building.Id].Status);

        using var me = await alice.GetAsync("/api/v1/users/me");
        using var login = await LoginAsync(factory, "alice@example.com");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        await AssertCodeAsync(login, HttpStatusCode.Forbidden, "account_suspended");

        using var unsuspend = await bob.PostAsJsonAsync($"{Admin}/users/{AliceId}/unsuspend", new { reason = "Apelación aceptada." });
        using var loginAgain = await LoginAsync(factory, "alice@example.com");
        Assert.Equal(HttpStatusCode.OK, unsuspend.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginAgain.StatusCode);
    }

    [Fact]
    public async Task Refresh_of_a_suspended_account_returns_403()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);
        using var anonymous = factory.CreateHttpsClient();
        using var login = await LoginAsync(factory, "carol@example.com");
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var refreshToken = document.RootElement.GetProperty("refreshToken").GetString();

        factory.Moderation.Suspensions[CarolId] = new UserSuspension(CarolId, "Prueba", "admin", DateTimeOffset.UtcNow);
        using var refresh = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

        await AssertCodeAsync(refresh, HttpStatusCode.Forbidden, "account_suspended");
    }

    [Fact]
    public async Task Buildings_can_be_suspended_only_when_verified_and_admins_cannot_be_suspended()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Admin);
        var building = factory.Buildings.Buildings.Values.Single();

        using var early = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/suspend", new { reason = "Contenido dudoso." });
        await AssertCodeAsync(early, HttpStatusCode.Conflict, "building_state");

        (await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version })).Dispose();
        using var suspended = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/suspend", new { reason = "Contenido dudoso." });
        Assert.Equal(BuildingStatus.Suspended, factory.Buildings.Buildings[building.Id].Status);
        using var unsuspended = await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/unsuspend", new { reason = "Revisado." });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unsuspended.StatusCode);
        Assert.Equal(BuildingStatus.Verified, factory.Buildings.Buildings[building.Id].Status);

        using var adminTarget = await bob.PostAsJsonAsync($"{Admin}/users/{BobId}/suspend", new { reason = "Intento de prueba." });
        await AssertCodeAsync(adminTarget, HttpStatusCode.Conflict, "user_state");
    }

    [Fact]
    public async Task Audit_log_is_listed_and_cannot_be_modified()
    {
        using var factory = new UserResourceFactory();
        using var alice = await PublishedBuildingOwnerAsync(factory);
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Admin);
        var building = factory.Buildings.Buildings.Values.Single();
        (await bob.PostAsJsonAsync($"{Admin}/buildings/{building.Id}/approve", new { version = building.Version })).Dispose();

        using var audit = await bob.GetAsync($"{Admin}/audit");
        using var delete = await bob.DeleteAsync($"{Admin}/audit");
        using var post = await bob.PostAsJsonAsync($"{Admin}/audit", new { action = "x" });

        using (var document = JsonDocument.Parse(await audit.Content.ReadAsStringAsync()))
        {
            var item = document.RootElement.GetProperty("items")[0];
            Assert.Equal("building.approved", item.GetProperty("action").GetString());
            Assert.Equal(new[] { "id", "actorId", "action", "targetType", "targetId", "reason", "createdAt" }, item.EnumerateObject().Select(property => property.Name));
        }

        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task Admin_limits_and_dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { AdminPerWindow = 1 });
        using var bob = await factory.ClientForAsync(BobId);
        await Grant(factory, BobId, StaffRoles.Admin);

        (await bob.GetAsync($"{Admin}/buildings")).Dispose();
        using var limited = await bob.GetAsync($"{Admin}/buildings");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        using var other = new UserResourceFactory();
        using var carol = await other.ClientForAsync(CarolId);
        other.Moderation.Unavailable = true;
        using var down = await carol.GetAsync($"{Admin}/buildings");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
    }

    // --- Soportes -------------------------------------------------

    private static Task Grant(UserResourceFactory factory, string userId, string role) =>
        factory.Moderation.GrantAsync(userId, role, "test", DateTimeOffset.UtcNow, CancellationToken.None);

    private static async Task<HttpClient> PublishedBuildingOwnerAsync(UserResourceFactory factory)
    {
        var alice = await factory.ClientForAsync(AliceId);
        (await alice.PostAsJsonAsync($"{Landlord}/profile", new { fullName = "Ana Arrendadora", phone = "+527731234567", rentalMode = "individual", acceptedTermsVersion = "2026-11" })).Dispose();
        using var created = await alice.PostAsJsonAsync($"{Landlord}/buildings", new
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
        (await alice.PostAsJsonAsync($"{Landlord}/buildings/{building.Id}/rooms/generate", new
        {
            floors = 1,
            roomsPerFloor = new[] { 2 },
            @base = new { price = 2800, sizeM2 = 12m, type = "single", bathroom = "shared", availableFrom = "2026-12-01", minContract = "six_months" },
        })).Dispose();
        using var published = await alice.PostAsync($"{Landlord}/buildings/{building.Id}/publish", null);
        Assert.Equal(HttpStatusCode.Accepted, published.StatusCode);
        return alice;
    }

    private static async Task<HttpResponseMessage> LoginAsync(UserResourceFactory factory, string email)
    {
        using var client = factory.CreateHttpsClient();
        return await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }
}
