using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-17 T01–T08: contrato HTTP de visitas, idempotencia, conflictos,
// concurrencia, BOLA, asignación masiva, rate limit y fallo cerrado.
public sealed class VisitsHttpTests
{
    private const string Base = "/api/v1/visits";
    private const string Mine = "/api/v1/users/me/visits";

    [Fact]
    public async Task Create_returns_201_with_contract_shape_in_utc()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), "Hola");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(
            new[] { "id", "property", "scheduledAt", "timeZone", "status", "message", "createdAt", "updatedAt", "cancelledAt" },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            new[] { "id", "title", "city" },
            root.GetProperty("property").EnumerateObject().Select(property => property.Name));
        Assert.Equal("requested", root.GetProperty("status").GetString());
        Assert.Equal("America/Mexico_City", root.GetProperty("timeZone").GetString());
        Assert.Equal(TimeSpan.Zero, root.GetProperty("scheduledAt").GetDateTimeOffset().Offset);
        Assert.Equal(17, root.GetProperty("scheduledAt").GetDateTimeOffset().Hour);
        Assert.DoesNotContain("dirección", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AliceId, Assert.Single(factory.Visits.Visits.Values).UserId);
    }

    [Fact]
    public async Task Same_key_and_body_replays_and_different_body_conflicts()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        var key = Guid.NewGuid();

        using var created = await CreateAsync(alice, key, PropertyA, Slot(3, 11), null);
        using var replay = await CreateAsync(alice, key, PropertyA, Slot(3, 11), null);
        using var reused = await CreateAsync(alice, key, PropertyB, Slot(4, 11), null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(await IdAsync(created), await IdAsync(replay));
        await AssertProblemAsync(reused, HttpStatusCode.Conflict, "idempotency_key_reused");
        Assert.Single(factory.Visits.Visits);
    }

    [Theory]
    [InlineData(null, "{\"propertyId\":\"0123456789876543000000a1\",\"scheduledAt\":\"SLOT\"}")]
    [InlineData("not-a-uuid", "{\"propertyId\":\"0123456789876543000000a1\",\"scheduledAt\":\"SLOT\"}")]
    [InlineData("KEY", "{\"propertyId\":\"0123456789876543000000a1\",\"scheduledAt\":\"2026-12-20T11:00:00\"}")]
    [InlineData("KEY", "{\"propertyId\":\"bad\",\"scheduledAt\":\"SLOT\"}")]
    [InlineData("KEY", "{\"propertyId\":\"0123456789876543000000a1\",\"scheduledAt\":\"SLOT\",\"userId\":\"665f1c2a9b1e4a0000000b0b\"}")]
    [InlineData("KEY", "{\"propertyId\":\"0123456789876543000000a1\",\"scheduledAt\":\"SLOT\",\"status\":\"confirmed\"}")]
    [InlineData("KEY", "not json")]
    public async Task Create_rejects_bad_input_with_400(string? key, string body)
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var request = new HttpRequestMessage(HttpMethod.Post, Base)
        {
            Content = new StringContent(body.Replace("SLOT", Slot(3, 11)), Encoding.UTF8, "application/json"),
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key == "KEY" ? Guid.NewGuid().ToString() : key);
        }

        using var response = await alice.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Visits.Visits);
    }

    [Fact]
    public async Task Create_rejects_unsupported_media_type_and_missing_property()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var request = new HttpRequestMessage(HttpMethod.Post, Base)
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var unsupported = await alice.SendAsync(request);
        using var missing = await CreateAsync(alice, Guid.NewGuid(), "0123456789876543ffffffff", Slot(3, 11), null);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(3, 7, 45, "outside_hours")]
    [InlineData(3, 20, 15, "outside_hours")]
    [InlineData(3, 11, 10, "not_quarter_hour")]
    [InlineData(61, 11, 0, "too_far")]
    public async Task Schedule_rules_return_422_with_code(int days, int hour, int minute, string code)
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(days, hour, minute), null);

        await AssertProblemAsync(response, (HttpStatusCode)422, code);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Solicitud no procesable", document.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Conflicts_return_409_with_specific_codes()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        (await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null)).Dispose();
        using var sameProperty = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(5, 11), null);
        using var overlap = await CreateAsync(alice, Guid.NewGuid(), PropertyB, Slot(3, 11, 45), null);
        using var apart = await CreateAsync(alice, Guid.NewGuid(), PropertyB, Slot(3, 12), null);

        await AssertProblemAsync(sameProperty, HttpStatusCode.Conflict, "active_visit_for_property");
        await AssertProblemAsync(overlap, HttpStatusCode.Conflict, "schedule_overlap");
        Assert.Equal(HttpStatusCode.Created, apart.StatusCode);
    }

    [Fact]
    public async Task Concurrent_requests_with_different_keys_create_only_one_visit()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        factory.Lock.HoldDelay = TimeSpan.FromMilliseconds(50);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.Created),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        Assert.Single(factory.Visits.Visits);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Other_users_visit_is_404_for_get_and_cancel()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);
        using var created = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null);
        var id = await IdAsync(created);

        using var get = await bob.GetAsync($"{Base}/{id}");
        using var cancel = await bob.PostAsync($"{Base}/{id}/cancel", null);
        using var list = await bob.GetAsync(Mine);
        using var invalid = await bob.GetAsync($"{Base}/not-an-id");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal("requested", Assert.Single(factory.Visits.Visits.Values).Status.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_list_filters_by_status()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var first = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null);
        using var second = await CreateAsync(alice, Guid.NewGuid(), PropertyB, Slot(2, 9), null);
        var id = await IdAsync(first);

        using var cancel = await alice.PostAsJsonAsync($"{Base}/{id}/cancel", new { reason = "Ya encontré cuarto" });
        using var again = await alice.PostAsync($"{Base}/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        using (var document = JsonDocument.Parse(await again.Content.ReadAsStringAsync()))
        {
            Assert.Equal("cancelled", document.RootElement.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, document.RootElement.GetProperty("cancelledAt").ValueKind);
        }

        using var all = await alice.GetAsync(Mine);
        using var cancelled = await alice.GetAsync($"{Mine}?status=cancelled");
        using var badStatus = await alice.GetAsync($"{Mine}?status=confirmed");
        using (var document = JsonDocument.Parse(await all.Content.ReadAsStringAsync()))
        {
            // Orden scheduledAt asc: la de pasado mañana va primero.
            Assert.Equal(
                new[] { await IdAsync(second), id },
                document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        }

        using (var document = JsonDocument.Parse(await cancelled.Content.ReadAsStringAsync()))
        {
            Assert.Equal(id, Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray()).GetProperty("id").GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [Fact]
    public async Task Daily_create_limit_returns_429_with_retry_after()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { VisitCreatesPerDay = 1 });
        using var alice = await factory.ClientForAsync(AliceId);

        using var created = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null);
        using var limited = await CreateAsync(alice, Guid.NewGuid(), PropertyB, Slot(4, 11), null);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Fails_closed_when_lock_or_storage_are_down()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        factory.Lock.Unavailable = true;
        using var lockDown = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), null);
        factory.Lock.Unavailable = false;
        factory.Visits.Unavailable = true;
        using var mongoDown = await alice.GetAsync(Mine);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, lockDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
    }

    [Fact]
    public async Task Requires_a_valid_bearer_token()
    {
        using var factory = new UserResourceFactory();
        using var anonymous = factory.CreateHttpsClient();

        using var response = await CreateAsync(anonymous, Guid.NewGuid(), PropertyA, Slot(3, 11), null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.Visits.Visits);
    }

    [Fact]
    public async Task Message_is_not_written_to_logs()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var created = await CreateAsync(alice, Guid.NewGuid(), PropertyA, Slot(3, 11), "mensaje-secreto-123");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("mensaje-secreto-123", Assert.Single(factory.Visits.Visits.Values).Message);
        Assert.NotEmpty(factory.Logs);
        Assert.DoesNotContain(factory.Logs, line => line.Contains("mensaje-secreto-123", StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs, line => line.Contains(PropertyA, StringComparison.Ordinal) && line.Contains(AliceId, StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        Guid key,
        string propertyId,
        string scheduledAt,
        string? message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Base)
        {
            Content = JsonContent.Create(new { propertyId, scheduledAt, message }),
        };
        request.Headers.Add("Idempotency-Key", key.ToString());
        return client.SendAsync(request);
    }

    private static async Task<string> IdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }
}
