using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-21 1.2.0 T01–T05: texto y fotos se revisan ANTES de guardar; lo
// inapropiado responde 422 y no se guarda; sin revisión, 503 (falla cerrado).
public sealed class ContentModerationHttpTests
{
    private const string Landlord = "/api/v1/landlord";
    private const string Bad = "Hola " + FakeContentSafetyClient.FlaggedMarker;

    [Fact]
    public async Task Offensive_text_is_rejected_before_saving_in_every_module()
    {
        using var factory = new UserResourceFactory();
        var (alice, buildingId, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var _ = alice;
        using var carol = await factory.ClientForAsync(CarolId);

        // Perfil (NV-11).
        await AssertRejectedAsync(await carol.PatchAsJsonAsync("/api/v1/users/me/profile", new { name = Bad }), "name", "hate");
        Assert.False(factory.Profiles.Profiles.TryGetValue(CarolId, out var profile) && profile.Name == Bad);

        // Edificio (NV-12).
        await AssertRejectedAsync(await alice.PatchAsJsonAsync($"{Landlord}/buildings/{buildingId}", new { name = Bad, version = factory.Buildings.Buildings[buildingId].Version }), "name", "hate");
        Assert.Equal("Casa Estudiantes", factory.Buildings.Buildings[buildingId].Name);

        // Reporte (NV-21 1.1.0).
        await AssertRejectedAsync(await carol.PostAsJsonAsync("/api/v1/reports", new { targetType = "building", targetId = buildingId, reason = "fraud", details = Bad }), "details", "hate");
        Assert.Empty(factory.Moderation.Reports);

        // Solicitud y chat (NV-16) sobre un cuarto publicado.
        factory.Buildings.Buildings[buildingId] = factory.Buildings.Buildings[buildingId] with { Status = BuildingStatus.Verified };
        factory.Rooms.Rooms[roomId] = factory.Rooms.Rooms[roomId] with { Status = RoomStatus.Available };
        await AssertRejectedAsync(await carol.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = Bad }), "message", "hate");
        Assert.Empty(factory.RentalRequests.Requests);
        Assert.Equal(HttpStatusCode.Created, (await carol.PostAsJsonAsync($"/api/v1/rooms/{roomId}/requests", new { message = "Hola, ¿sigue disponible?" })).StatusCode);
        var conversationId = Assert.Single(factory.Conversations.Conversations.Values).Id;
        var before = factory.Conversations.Messages.Count;
        await AssertRejectedAsync(await carol.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { text = Bad, clientMessageId = Guid.NewGuid() }), "text", "hate");
        Assert.Equal(before, factory.Conversations.Messages.Count);

        // Cada rechazo queda registrado sin el contenido.
        Assert.Equal(5, factory.Rejections.Items.Count);
        Assert.All(factory.Rejections.Items, item => Assert.DoesNotContain(FakeContentSafetyClient.FlaggedMarker, JsonSerializer.Serialize(item), StringComparison.Ordinal));
        Assert.Contains(factory.Rejections.Items, item => item.Source == "chat.text" && item.UserId == CarolId && item.MaxSeverity == 6);
    }

    [Fact]
    public async Task Blocklisted_words_are_rejected_even_when_the_cloud_says_it_is_clean()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);

        await AssertRejectedAsync(await carol.PatchAsJsonAsync("/api/v1/users/me/profile", new { name = "Eres un PENDÉJO" }), "name", "blocklist");
        Assert.Equal(HttpStatusCode.OK, (await carol.PatchAsJsonAsync("/api/v1/users/me/profile", new { name = "Carolina Díaz" })).StatusCode);
        Assert.Equal("Carolina Díaz", factory.Profiles.Profiles[CarolId].Name);
    }

    [Fact]
    public async Task Photos_are_reviewed_before_saving_but_documents_never_leave()
    {
        using var factory = new UserResourceFactory();
        var (alice, buildingId, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var _ = alice;
        factory.ContentSafety.FlagImages = true;

        await AssertRejectedAsync(await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(), "cuarto.png")), "file", "sexual");
        await AssertRejectedAsync(await alice.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File(Png(), "fachada.png")), "file", "sexual");
        Assert.Empty(factory.Media.Photos);

        // Los documentos de verificación (INE) son privados: no se envían a la nube.
        var calls = factory.ContentSafety.ImageCalls;
        Assert.True((await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/ine", File("%PDF-1.4\n%%EOF\n"u8.ToArray(), "ine.pdf"))).IsSuccessStatusCode);
        Assert.Equal(calls, factory.ContentSafety.ImageCalls);

        factory.ContentSafety.FlagImages = false;
        Assert.Equal(HttpStatusCode.Created, (await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(), "cuarto.png"))).StatusCode);
    }

    [Fact]
    public async Task Without_the_review_nothing_is_saved()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);
        factory.ContentSafety.Unavailable = true;

        using var response = await carol.PatchAsJsonAsync("/api/v1/users/me/profile", new { name = "Carolina" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("30", response.Headers.RetryAfter?.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("moderation_unavailable", document.RootElement.GetProperty("code").GetString());
        Assert.False(factory.Profiles.Profiles.ContainsKey(CarolId));
    }

    [Fact]
    public async Task Staff_see_rejections_without_content_and_others_cannot()
    {
        using var factory = new UserResourceFactory();
        using var carol = await factory.ClientForAsync(CarolId);
        using var bob = await factory.ClientForAsync(BobId);
        (await carol.PatchAsJsonAsync("/api/v1/users/me/profile", new { name = Bad })).Dispose();
        await factory.Moderation.GrantAsync(BobId, StaffRoles.Reviewer, "test", DateTimeOffset.UtcNow, CancellationToken.None);

        using var list = await bob.GetAsync($"/api/v1/admin/moderation/rejections?userId={CarolId}&kind=text");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var raw = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(FakeContentSafetyClient.FlaggedMarker, raw, StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(raw))
        {
            var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray().ToArray());
            Assert.Equal(["id", "userId", "kind", "field", "source", "categories", "maxSeverity", "createdAt"], item.EnumerateObject().Select(property => property.Name));
            Assert.Equal("profile.name", item.GetProperty("source").GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync("/api/v1/admin/moderation/rejections?kind=video")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await carol.GetAsync("/api/v1/admin/moderation/rejections")).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static async Task AssertRejectedAsync(HttpResponseMessage response, string field, string category)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.Equal("content_rejected", root.GetProperty("code").GetString());
            Assert.Equal(field, root.GetProperty("field").GetString());
            Assert.Contains(category, root.GetProperty("categories").EnumerateArray().Select(item => item.GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
            Assert.DoesNotContain(FakeContentSafetyClient.FlaggedMarker, root.GetRawText(), StringComparison.Ordinal);
        }
    }

    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(800, 600);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(200, 120, 30));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static MultipartFormDataContent File(byte[] content, string name)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", name);
        return form;
    }

    private static async Task<(HttpClient Alice, string BuildingId, string RoomId)> DraftBuildingOfAliceAsync(UserResourceFactory factory)
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
        var buildingId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
        (await alice.PostAsJsonAsync($"{Landlord}/buildings/{buildingId}/rooms/generate", new
        {
            floors = 1,
            roomsPerFloor = new[] { 1 },
            @base = new { price = 2800, sizeM2 = 12m, type = "single", bathroom = "shared", availableFrom = "2026-12-01", minContract = "six_months" },
        })).Dispose();
        return (alice, buildingId, factory.Rooms.Rooms.Values.Single(room => room.BuildingId == buildingId).Id);
    }
}
