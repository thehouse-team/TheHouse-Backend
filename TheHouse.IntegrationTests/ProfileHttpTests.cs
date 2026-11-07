using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TheHouse.Application.Common;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-11 T01–T03, T07: perfil, universidad y preferencias por HTTP con dos
// usuarios, validación estricta, límites, fallo cerrado y logs sin PII.
public sealed class ProfileHttpTests
{
    private const string ProfilePath = "/api/v1/users/me/profile";
    private const string UniversityPath = "/api/v1/users/me/university";
    private const string PreferencesPath = "/api/v1/users/me/preferences";

    [Fact]
    public async Task Empty_profile_and_default_preferences_have_contract_shape()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var profile = await alice.GetAsync(ProfilePath);
        using var preferences = await alice.GetAsync(PreferencesPath);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("no-store", profile.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await profile.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { "name", "university", "quickSetupCompleted", "updatedAt" },
                document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("university").ValueKind);
            Assert.False(document.RootElement.GetProperty("quickSetupCompleted").GetBoolean());
        }

        Assert.Equal(HttpStatusCode.OK, preferences.StatusCode);
        using (var document = JsonDocument.Parse(await preferences.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(
                new[] { "budget", "maxDistanceKm", "essentialServices", "livingWith", "placeType", "pets", "transport", "propertyTypes", "quickSetupCompleted", "version", "updatedAt" },
                root.EnumerateObject().Select(property => property.Name));
            Assert.Equal("MXN", root.GetProperty("budget").GetProperty("currency").GetString());
            Assert.Equal(0, root.GetProperty("version").GetInt64());
            Assert.Empty(root.GetProperty("essentialServices").EnumerateArray());
        }
    }

    [Fact]
    public async Task Name_is_updated_and_validated()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var updated = await alice.PatchAsync(ProfilePath, JsonContent.Create(new { name = "  Ana López  " }));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using (var document = JsonDocument.Parse(await updated.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Ana López", document.RootElement.GetProperty("name").GetString());
        }

        foreach (var body in new object[] { new { name = "" }, new { name = new string('a', 81) }, new { name = "Ana\u0007" }, new { name = "Ana", userId = BobId } })
        {
            using var rejected = await alice.PatchAsync(ProfilePath, JsonContent.Create(body));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        using var wrongType = await alice.PatchAsync(ProfilePath, new StringContent("{\"name\":\"Ana\"}", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        Assert.Equal("Ana López", factory.Profiles.Profiles[AliceId].Name);
    }

    [Fact]
    public async Task University_is_saved_idempotently_and_shown_in_profile()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        var uttt = factory.Universities.Universities.Single(university => university.Code == "UTTT");

        using var first = await alice.PutAsJsonAsync(UniversityPath, new { universityId = uttt.Id });
        using var again = await alice.PutAsJsonAsync(UniversityPath, new { universityId = uttt.Id });
        using var missing = await alice.PutAsJsonAsync(UniversityPath, new { universityId = "0123456789abcdffffffffff" });
        using var invalid = await alice.PutAsJsonAsync(UniversityPath, new { universityId = "nope" });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var profile = await alice.GetAsync(ProfilePath);
        using var document = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        var university = document.RootElement.GetProperty("university");
        Assert.Equal(new[] { "id", "code", "name", "city" }, university.EnumerateObject().Select(property => property.Name));
        Assert.Equal("UTTT", university.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Preferences_replace_increments_version_and_derives_quick_setup()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var partial = await alice.PutAsJsonAsync(PreferencesPath, new { budget = new { max = 3000 } });
        using var full = await alice.PutAsJsonAsync(PreferencesPath, ValidPreferences());

        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        using (var document = JsonDocument.Parse(await partial.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, document.RootElement.GetProperty("version").GetInt64());
            Assert.False(document.RootElement.GetProperty("quickSetupCompleted").GetBoolean());
        }

        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        using (var document = JsonDocument.Parse(await full.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(2, root.GetProperty("version").GetInt64());
            Assert.True(root.GetProperty("quickSetupCompleted").GetBoolean());
            Assert.Equal(new[] { "wifi", "water" }, root.GetProperty("essentialServices").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(2.5m, root.GetProperty("maxDistanceKm").GetDecimal());
        }

        using var profile = await alice.GetAsync(ProfilePath);
        using var profileDocument = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.True(profileDocument.RootElement.GetProperty("quickSetupCompleted").GetBoolean());
    }

    [Theory]
    [InlineData("{\"quickSetupCompleted\":true}")]
    [InlineData("{\"version\":99}")]
    [InlineData("{\"userId\":\"665f1c2a9b1e4a0000000b0b\"}")]
    [InlineData("{\"essentialServices\":[\"pool\"]}")]
    [InlineData("{\"essentialServices\":[\"wifi\",\"wifi\"]}")]
    [InlineData("{\"livingWith\":\"mixed\"}")]
    [InlineData("{\"placeType\":\"loft\"}")]
    [InlineData("{\"transport\":\"plane\"}")]
    [InlineData("{\"propertyTypes\":[\"castle\"]}")]
    [InlineData("{\"budget\":{\"min\":5000,\"max\":3000}}")]
    [InlineData("{\"budget\":{\"max\":50001}}")]
    [InlineData("{\"budget\":{\"max\":3000,\"currency\":\"USD\"}}")]
    [InlineData("{\"maxDistanceKm\":0.4}")]
    [InlineData("{\"maxDistanceKm\":30.5}")]
    [InlineData("{\"maxDistanceKm\":1.25}")]
    [InlineData("not json")]
    public async Task Preferences_reject_invalid_or_server_owned_fields(string body)
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PutAsync(PreferencesPath, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.PreferencesStore.Preferences);
    }

    [Fact]
    public async Task Users_only_see_their_own_profile_and_preferences()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        (await alice.PatchAsync(ProfilePath, JsonContent.Create(new { name = "Alice" }))).Dispose();
        (await alice.PutAsJsonAsync(PreferencesPath, ValidPreferences())).Dispose();

        using var bobProfile = await bob.GetAsync(ProfilePath);
        using var bobPreferences = await bob.GetAsync(PreferencesPath);

        using (var document = JsonDocument.Parse(await bobProfile.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("name").ValueKind);
        }

        using (var document = JsonDocument.Parse(await bobPreferences.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, document.RootElement.GetProperty("version").GetInt64());
        }
    }

    [Theory]
    [InlineData("GET", ProfilePath)]
    [InlineData("PATCH", ProfilePath)]
    [InlineData("PUT", UniversityPath)]
    [InlineData("GET", PreferencesPath)]
    [InlineData("PUT", PreferencesPath)]
    public async Task Requires_a_valid_bearer_token(string method, string path)
    {
        using var factory = new UserResourceFactory();
        using var anonymous = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "GET" ? null : JsonContent.Create(new { }),
        };

        using var response = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rate_limit_returns_429_and_dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory(new UserRateLimitOptions { ProfilePerWindow = 2 });
        using var alice = await factory.ClientForAsync(AliceId);

        (await alice.GetAsync(ProfilePath)).Dispose();
        (await alice.GetAsync(PreferencesPath)).Dispose();
        using var limited = await alice.GetAsync(ProfilePath);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("60", limited.Headers.GetValues("Retry-After").Single());

        using var other = new UserResourceFactory();
        using var bob = await other.ClientForAsync(BobId);
        other.PreferencesStore.Unavailable = true;
        using var mongoDown = await bob.PutAsJsonAsync(PreferencesPath, ValidPreferences());
        other.PreferencesStore.Unavailable = false;
        other.RateLimit.Unavailable = true;
        using var redisDown = await bob.GetAsync(ProfilePath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, mongoDown.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);
    }

    [Fact]
    public async Task Name_is_not_written_to_logs()
    {
        using var factory = new UserResourceFactory();
        using var alice = await factory.ClientForAsync(AliceId);

        using var response = await alice.PatchAsync(ProfilePath, JsonContent.Create(new { name = "NombreSecreto987" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(factory.Logs);
        Assert.DoesNotContain(factory.Logs, line => line.Contains("NombreSecreto987", StringComparison.Ordinal));
    }

    private static object ValidPreferences() => new
    {
        budget = new { min = 1500, max = 3500, currency = "MXN" },
        maxDistanceKm = 2.5m,
        essentialServices = new[] { "wifi", "water" },
        livingWith = "any",
        placeType = "room",
        pets = false,
        transport = "public",
        propertyTypes = new[] { "house" },
    };
}
