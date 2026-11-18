using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-22 T01–T06: contrato HTTP del perfil de compañero, lista recíproca,
// contacto por una conversación `roommate` de NV-16, aviso de NV-20,
// privacidad, validación y fallo cerrado.
public sealed class RoommatesHttpTests
{
    private const string Mine = "/api/v1/users/me/roommate-profile";
    private const string List = "/api/v1/roommates";

    [Fact]
    public async Task Two_students_find_each_other_and_chat()
    {
        using var factory = new UserResourceFactory();
        var uttt = Seed(factory);
        using var alice = await factory.ClientForAsync(AliceId);
        using var bob = await factory.ClientForAsync(BobId);

        using var created = await alice.PutAsJsonAsync(Mine, Body("night"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString());
        using (var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal(
                new[] { "id", "visible", "budgetMin", "budgetMax", "zone", "moveIn", "durationMonths", "hasPlace", "habits", "about", "university", "expiresAt", "createdAt", "updatedAt" },
                document.RootElement.EnumerateObject().Select(property => property.Name));
            Assert.Equal(uttt, document.RootElement.GetProperty("university").GetProperty("id").GetString());
        }

        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync(Mine, Body("night"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await bob.PutAsJsonAsync(Mine, Body("night"))).StatusCode);

        using var list = await alice.GetAsync($"{List}?budgetMax=3000&zone=walking");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        string bobProfile;
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var card = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(
                new[] { "id", "name", "university", "budgetMin", "budgetMax", "zone", "moveIn", "durationMonths", "hasPlace", "habits", "about", "matches", "updatedAt" },
                card.EnumerateObject().Select(property => property.Name));
            Assert.Equal("Bob", card.GetProperty("name").GetString());
            Assert.Equal(7, card.GetProperty("matches").GetInt32());
            Assert.DoesNotContain(BobId, document.RootElement.GetRawText());
            bobProfile = card.GetProperty("id").GetString()!;
        }

        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync($"{List}/{bobProfile}")).StatusCode);
        using var contact = await alice.PostAsJsonAsync($"{List}/{bobProfile}/contact", new { message = "Hola Bob, ¿buscas roomie?" });
        Assert.Equal(HttpStatusCode.Created, contact.StatusCode);
        var conversationId = JsonDocument.Parse(await contact.Content.ReadAsStringAsync()).RootElement.GetProperty("conversationId").GetString()!;
        using var again = await alice.PostAsJsonAsync($"{List}/{bobProfile}/contact", new { message = "¿Sigues buscando?" });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(conversationId, JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement.GetProperty("conversationId").GetString());

        using (var conversations = JsonDocument.Parse(await (await bob.GetAsync("/api/v1/conversations")).Content.ReadAsStringAsync()))
        {
            var item = Assert.Single(conversations.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal("roommate", item.GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("room").ValueKind);
            Assert.Equal("Alice", item.GetProperty("counterpart").GetProperty("name").GetString());
            Assert.Equal(2, item.GetProperty("unreadCount").GetInt32());
        }

        using var reply = await bob.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { text = "¡Sí! Hablemos.", clientMessageId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var notice = Assert.Single(factory.Notifications.Items, item => item.UserId == BobId);
        Assert.Equal(NotificationTypes.MessageReceived, notice.Type);
        Assert.Equal(2, notice.Count);
        Assert.Equal("Alice te escribió sobre tu búsqueda de compañero.", notice.Body);
    }

    [Fact]
    public async Task List_requires_own_profile_university_and_hides_other_universities()
    {
        using var factory = new UserResourceFactory();
        Seed(factory);
        factory.Profiles.Profiles[CarolId] = new UserProfile { UserId = CarolId, Name = "Carol" };
        using var alice = await factory.ClientForAsync(AliceId);
        using var carol = await factory.ClientForAsync(CarolId);

        await AssertCodeAsync(await alice.GetAsync(List), HttpStatusCode.Forbidden, "roommate_profile_required");
        await AssertCodeAsync(await carol.PutAsJsonAsync(Mine, Body("night")), HttpStatusCode.Conflict, "university_required");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync(Mine, new { visible = true, userId = BobId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsJsonAsync(Mine, Body("lunes"))).StatusCode);

        (await alice.PutAsJsonAsync(Mine, Body("night"))).Dispose();
        factory.Profiles.Profiles[CarolId] = new UserProfile { UserId = CarolId, Name = "Carol", UniversityId = "6650000000000000000000ff" };
        (await carol.PutAsJsonAsync(Mine, Body("night"))).Dispose();
        var carolProfile = factory.Roommates.Profiles.Values.Single(profile => profile.UserId == CarolId).Id;
        var aliceProfile = factory.Roommates.Profiles.Values.Single(profile => profile.UserId == AliceId).Id;

        using var list = await alice.GetAsync(List);
        Assert.Equal(0, JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement.GetProperty("totalItems").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"{List}/{carolProfile}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync($"{List}/{carolProfile}/contact", new { message = "Hola" })).StatusCode);
        await AssertCodeAsync(await alice.PostAsJsonAsync($"{List}/{aliceProfile}/contact", new { message = "Yo" }), HttpStatusCode.Conflict, "own_profile");

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync(Mine)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync(Mine)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(List)).StatusCode);
    }

    [Fact]
    public async Task Dependencies_fail_closed()
    {
        using var factory = new UserResourceFactory();
        Seed(factory);
        using var alice = await factory.ClientForAsync(AliceId);

        factory.Roommates.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync(Mine)).StatusCode);
        factory.Roommates.Unavailable = false;
        factory.RateLimit.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.GetAsync(List)).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static string Seed(UserResourceFactory factory)
    {
        var uttt = factory.Universities.Universities.Single(university => university.Code == "UTTT").Id;
        factory.Profiles.Profiles[AliceId] = new UserProfile { UserId = AliceId, Name = "Alice Martínez", UniversityId = uttt };
        factory.Profiles.Profiles[BobId] = new UserProfile { UserId = BobId, Name = "Bob Pérez", UniversityId = uttt };
        return uttt;
    }

    private static object Body(string schedule) =>
        new
        {
            visible = true,
            budgetMin = 1500,
            budgetMax = 2500,
            zone = "walking",
            moveIn = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            durationMonths = 4,
            hasPlace = false,
            habits = new { schedule, cleaning = "weekly", noise = "quiet", smoking = "no", pets = "none", social = "occasional" },
            about = "Estudio Software, soy ordenado.",
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
}
