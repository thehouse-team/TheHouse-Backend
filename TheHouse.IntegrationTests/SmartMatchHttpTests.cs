using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.RoomTestFixtures;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-23 1.2.0 T03–T05, T08–T10, T12 y T13: Smart Match por HTTP sobre los 12
// cuartos de demostración (edificios `any`) con el modelo base `baseline-1.0.0`.
public sealed class SmartMatchHttpTests
{
    private const string Scores = "/api/v1/smart-match/scores";
    private const string Recommendations = "/api/v1/smart-match/recommendations";
    private const string Query = "/api/v1/smart-match/recommendations/query";
    private const string Feedback = "/api/v1/smart-match/feedback";
    private const string Dismissals = "/api/v1/smart-match/dismissals";
    private const string Metrics = "/api/v1/admin/smart-match/metrics";

    [Fact]
    public async Task Guest_scores_have_the_contract_shape_and_match_the_model()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var rooms = RoomIds(factory);

        using var response = await client.PostAsJsonAsync(Scores, new
        {
            roomIds = new[] { rooms[0], rooms[1], MissingId },
            universityId = Uttt(factory),
            preferences = Preferences(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(
            new[] { "roomId", "eligible", "excludedBy", "matchScore", "breakdown", "modelVersion", "featureSchemaVersion" },
            items[0].EnumerateObject().Select(property => property.Name));
        Assert.True(items[0].GetProperty("eligible").GetBoolean());
        Assert.Equal("baseline-1.0.0", items[0].GetProperty("modelVersion").GetString());
        Assert.Equal("2.0.0", items[0].GetProperty("featureSchemaVersion").GetString());
        var breakdown = items[0].GetProperty("breakdown").EnumerateArray().ToArray();
        Assert.Equal(new[] { "factor", "label", "points", "maxPoints" }, breakdown[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            new[] { "budget", "distance", "essential_services", "place_type", "property_type", "extras" },
            breakdown.Select(factor => factor.GetProperty("factor").GetString()));

        // El mismo cálculo fuera de HTTP: celda aproximada → campus de la universidad del estudiante.
        Assert.Equal(Expected(factory, rooms[0], Preferences()), items[0].GetProperty("matchScore").GetInt32());

        var missing = items[2];
        Assert.False(missing.GetProperty("eligible").GetBoolean());
        Assert.Equal(new[] { "unavailable" }, missing.GetProperty("excludedBy").EnumerateArray().Select(reason => reason.GetString()));
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("matchScore").ValueKind);
        Assert.Empty(missing.GetProperty("breakdown").EnumerateArray());
    }

    [Fact]
    public async Task Hard_constraints_exclude_with_every_reason_and_living_with_is_strict()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var rooms = RoomIds(factory);

        using var response = await client.PostAsJsonAsync(Scores, new
        {
            roomIds = rooms.Take(3).ToArray(),
            universityId = Uttt(factory),
            preferences = Preferences(budgetMin: 0, budgetMax: 600, livingWith: "women"),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.All(document.RootElement.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.False(item.GetProperty("eligible").GetBoolean());
            Assert.Equal(new[] { "budget", "living_with" }, item.GetProperty("excludedBy").EnumerateArray().Select(reason => reason.GetString()));
        });
    }

    [Fact]
    public async Task Session_uses_saved_preferences_and_rejects_preferences_in_the_body()
    {
        using var factory = await SeededAsync();
        await SaveProfileAsync(factory, AliceId);
        using var alice = await factory.ClientForAsync(AliceId);
        var rooms = RoomIds(factory);

        using var ok = await alice.PostAsJsonAsync(Scores, new { roomIds = new[] { rooms[0] } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var document = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
        Assert.Equal(Expected(factory, rooms[0], Preferences()), document.RootElement.GetProperty("items")[0].GetProperty("matchScore").GetInt32());

        using var mixed = await alice.PostAsJsonAsync(Scores, new { roomIds = new[] { rooms[0] }, preferences = Preferences() });
        Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);
    }

    [Fact]
    public async Task Incomplete_profile_is_422_with_the_missing_fields()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        using var client = factory.CreateHttpsClient();

        using var mine = await alice.GetAsync(Recommendations);
        await AssertIncompleteAsync(mine, "university", "budget.max", "livingWith", "placeType");

        using var guest = await client.PostAsJsonAsync(Query, new { universityId = Uttt(factory), preferences = new { budget = new { max = 5000 } } });
        await AssertIncompleteAsync(guest, "livingWith", "placeType");
    }

    [Fact]
    public async Task Recommendations_are_ordered_paginated_and_dismissals_go_last()
    {
        using var factory = await SeededAsync();
        await SaveProfileAsync(factory, AliceId);
        using var alice = await factory.ClientForAsync(AliceId);

        var first = await RecommendAsync(alice, Recommendations + "?pageSize=50");
        Assert.NotEmpty(first);
        Assert.Equal(first.OrderByDescending(item => item.Score).Select(item => item.Score), first.Select(item => item.Score));
        Assert.All(first, item => Assert.False(item.Dismissed));

        var top = first[0].RoomId;
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Dismissals, new { roomId = top })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Dismissals, new { roomId = top })).StatusCode);
        var dismissed = await RecommendAsync(alice, Recommendations + "?pageSize=50");
        Assert.Equal(top, dismissed[^1].RoomId);
        Assert.True(dismissed[^1].Dismissed);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"{Dismissals}/{top}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"{Dismissals}/{top}")).StatusCode);
        Assert.Equal(top, (await RecommendAsync(alice, Recommendations + "?pageSize=50"))[0].RoomId);

        using var page = await alice.GetAsync(Recommendations + "?page=2&pageSize=1");
        using var document = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        Assert.Equal(first.Count, document.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(first[1].RoomId, document.RootElement.GetProperty("items")[0].GetProperty("room").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Guest_query_works_without_session_and_unknown_university_is_404()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();

        var items = await RecommendAsync(client, Query + "?pageSize=50", new { universityId = Uttt(factory), preferences = Preferences() });
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.False(item.Dismissed));

        using var unknown = await client.PostAsJsonAsync(Query, new { universityId = MissingId, preferences = Preferences() });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Feedback_replaces_the_previous_opinion_and_requires_a_published_room()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Feedback, new { roomId = room, useful = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync(Feedback, new { roomId = room, useful = false })).StatusCode);
        var saved = Assert.Single(factory.SmartMatch.Feedback);
        Assert.False(saved.Useful);
        Assert.Equal("baseline-1.0.0", saved.ModelVersion);

        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync(Feedback, new { roomId = MissingId, useful = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync(Dismissals, new { roomId = MissingId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync(Feedback, new { roomId = room })).StatusCode);
    }

    // T12: BOLA. Cada usuario solo ve y deshace sus propios descartes.
    [Fact]
    public async Task Dismissals_belong_to_their_owner()
    {
        using var factory = await SeededAsync();
        await SaveProfileAsync(factory, AliceId);
        await SaveProfileAsync(factory, BobId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        var top = (await RecommendAsync(alice, Recommendations + "?pageSize=50"))[0].RoomId;

        (await alice.PostAsJsonAsync(Dismissals, new { roomId = top })).Dispose();
        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"{Dismissals}/{top}")).StatusCode);

        Assert.True((await RecommendAsync(alice, Recommendations + "?pageSize=50")).Single(item => item.RoomId == top).Dismissed);
        Assert.All(await RecommendAsync(bob, Recommendations + "?pageSize=50"), item => Assert.False(item.Dismissed));
    }

    // T12: contratos estrictos y validación de límites.
    [Theory]
    [InlineData("""{"roomIds":[],"universityId":"UNI","preferences":{"budget":{"max":5000},"livingWith":"any","placeType":"room"}}""")]
    [InlineData("""{"roomIds":["x"],"universityId":"UNI","preferences":{"budget":{"max":5000},"livingWith":"any","placeType":"room"}}""")]
    [InlineData("""{"roomIds":["ffffffffffffffffffffffff"],"universityId":"UNI","preferences":{"budget":{"max":5000},"livingWith":"any","placeType":"room"},"modelVersion":"x"}""")]
    [InlineData("""{"roomIds":["ffffffffffffffffffffffff"],"universityId":"UNI","preferences":{"budget":{"max":5000},"livingWith":"any","placeType":"room","weights":{"budget":100}}}""")]
    [InlineData("""{"roomIds":["ffffffffffffffffffffffff"],"universityId":"UNI","preferences":{"budget":{"max":5000},"livingWith":"robots","placeType":"room"}}""")]
    public async Task Invalid_or_extra_fields_are_400(string body)
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsync(Scores, new StringContent(body.Replace("UNI", Uttt(factory)), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task More_than_fifty_rooms_is_400_and_a_bad_token_is_401()
    {
        using var factory = await SeededAsync();
        using var client = factory.CreateHttpsClient();
        var ids = Enumerable.Range(0, 51).Select(index => index.ToString("x24")).ToArray();

        using var tooMany = await client.PostAsJsonAsync(Scores, new { roomIds = ids, universityId = Uttt(factory), preferences = Preferences() });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, Scores) { Content = JsonContent.Create(new { roomIds = new[] { MissingId } }) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-token");
        using var badToken = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, badToken.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Recommendations)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Feedback, new { roomId = MissingId, useful = true })).StatusCode);
    }

    [Fact]
    public async Task Rate_limits_apply_per_ip_and_per_user()
    {
        using var factory = await SeededAsync(new UserRateLimitOptions { SmartMatchGuestPerWindow = 1, SmartMatchPerWindow = 1 });
        using var client = factory.CreateHttpsClient();
        using var alice = await factory.ClientForAsync(AliceId);
        var body = new { universityId = Uttt(factory), preferences = Preferences() };

        (await client.PostAsJsonAsync(Query, body)).Dispose();
        using var limited = await client.PostAsJsonAsync(Query, body);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);

        (await alice.PostAsJsonAsync(Feedback, new { roomId = RoomIds(factory)[0], useful = true })).Dispose();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await alice.PostAsJsonAsync(Feedback, new { roomId = RoomIds(factory)[0], useful = true })).StatusCode);
    }

    // Fallo cerrado: Redis o MongoDB caídos → 503; las métricas caídas no afectan la respuesta.
    [Fact]
    public async Task Dependencies_down_fail_closed_but_metrics_failures_are_ignored()
    {
        using var factory = await SeededAsync();
        await SaveProfileAsync(factory, AliceId);
        using var alice = await factory.ClientForAsync(AliceId);

        factory.SmartMatch.StatsUnavailable = true;
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync(Recommendations)).StatusCode);
        Assert.Empty(factory.SmartMatch.Stats);

        factory.SmartMatch.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync(Recommendations)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.PostAsJsonAsync(Dismissals, new { roomId = RoomIds(factory)[0] })).StatusCode);
        factory.SmartMatch.Unavailable = false;

        factory.RateLimit.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync(Recommendations)).StatusCode);
    }

    // T10 (RF-66): solo admin; agregados sin ids.
    [Fact]
    public async Task Metrics_are_admin_only_and_aggregate_without_ids()
    {
        using var factory = await SeededAsync();
        await SaveProfileAsync(factory, AliceId);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var guest = factory.CreateHttpsClient();
        var rooms = RoomIds(factory);

        (await alice.GetAsync(Recommendations)).Dispose();
        (await guest.PostAsJsonAsync(Scores, new { roomIds = rooms.Take(2).ToArray(), universityId = Uttt(factory), preferences = Preferences() })).Dispose();
        (await alice.PostAsJsonAsync(Feedback, new { roomId = rooms[0], useful = true })).Dispose();
        (await alice.PostAsJsonAsync(Dismissals, new { roomId = rooms[1] })).Dispose();

        Assert.Equal(HttpStatusCode.Forbidden, (await bob.GetAsync(Metrics)).StatusCode);
        await factory.Moderation.GrantAsync(BobId, StaffRoles.Admin, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync(Metrics + "?from=2026-01-01&to=2026-12-31")).StatusCode);

        using var response = await bob.GetAsync(Metrics);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var totals = document.RootElement.GetProperty("totals");
        Assert.Equal(1, totals.GetProperty("scoreRequests").GetInt64());
        Assert.Equal(1, totals.GetProperty("recommendationRequests").GetInt64());
        Assert.Equal(1, totals.GetProperty("guestRequests").GetInt64());
        Assert.Equal(1, totals.GetProperty("feedbackUseful").GetInt64());
        Assert.Equal(1, totals.GetProperty("activeDismissals").GetInt64());
        Assert.Equal("baseline-1.0.0", document.RootElement.GetProperty("byModelVersion")[0].GetProperty("modelVersion").GetString());
        foreach (var forbidden in new[] { AliceId, BobId, rooms[0], rooms[1], "userId", "roomId" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);
        }
    }

    // NV-10: eliminar la cuenta borra la retroalimentación y los descartes.
    [Fact]
    public async Task Account_deletion_erases_smart_match_data()
    {
        using var factory = await SeededAsync();
        using var alice = await factory.ClientForAsync(AliceId);
        var room = RoomIds(factory)[0];
        (await alice.PostAsJsonAsync(Feedback, new { roomId = room, useful = true })).Dispose();
        (await alice.PostAsJsonAsync(Dismissals, new { roomId = room })).Dispose();

        using var deletion = await alice.PostAsJsonAsync("/api/v1/users/me/deletion", new { password = Password, confirmation = AccountDeletionRules.Confirmation });

        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Empty(factory.SmartMatch.Feedback);
        Assert.Empty(factory.SmartMatch.Dismissals);
    }

    private static object Preferences(int budgetMin = 1000, int budgetMax = 10000, string livingWith = "any") => new
    {
        budget = new { min = budgetMin, max = budgetMax },
        maxDistanceKm = 30,
        essentialServices = new[] { "wifi", "furnished" },
        livingWith,
        placeType = "room",
        transport = "public",
    };

    private static string Uttt(UserResourceFactory factory) =>
        factory.Universities.Universities.Single(university => university.Code == "UTTT").Id;

    private static async Task SaveProfileAsync(UserResourceFactory factory, string userId)
    {
        await factory.Profiles.SaveUniversityAsync(userId, Uttt(factory), DateTimeOffset.UtcNow, CancellationToken.None);
        await factory.PreferencesStore.ReplaceAsync(
            new UserPreferences
            {
                UserId = userId,
                BudgetMin = 1000,
                BudgetMax = 10000,
                MaxDistanceKm = 30,
                EssentialServices = ["wifi", "furnished"],
                LivingWith = "any",
                PlaceType = "room",
                Transport = "public",
            },
            CancellationToken.None);
    }

    private static int Expected(UserResourceFactory factory, string roomId, object _)
    {
        var room = factory.Rooms.Rooms[roomId];
        var building = factory.Buildings.Buildings[room.BuildingId];
        var university = factory.Universities.Universities.Single(item => item.Code == "UTTT");
        var (lat, lng) = ApproximateLocation.Cell(building.Latitude, building.Longitude);
        var distance = ListingRules.DistanceKm(lat, lng, university.Latitude!.Value, university.Longitude!.Value);
        var (student, _) = SmartMatchRules.Resolve(university.Id, new UserPreferences
        {
            BudgetMin = 1000,
            BudgetMax = 10000,
            MaxDistanceKm = 30,
            EssentialServices = ["wifi", "furnished"],
            LivingWith = "any",
            PlaceType = "room",
            Transport = "public",
        });
        return new BaselineCompatibilityModel().Score(SmartMatchRules.Features(student!, SmartMatchRules.Translate(room, building, distance))).MatchScore;
    }

    private static async Task<IReadOnlyList<(string RoomId, int Score, bool Dismissed)>> RecommendAsync(HttpClient client, string url, object? body = null)
    {
        using var response = body is null ? await client.GetAsync(url) : await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. document.RootElement.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("room").GetProperty("id").GetString()!,
            item.GetProperty("matchScore").GetInt32(),
            item.GetProperty("dismissed").GetBoolean()))];
    }

    private static async Task AssertIncompleteAsync(HttpResponseMessage response, params string[] missing)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("smart_match_profile_incomplete", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(missing, document.RootElement.GetProperty("missing").EnumerateArray().Select(item => item.GetString()));
    }
}
