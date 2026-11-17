using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using static TheHouse.IntegrationTests.UserResourceFactory;

namespace TheHouse.IntegrationTests;

// NV-13 T01–T06: fotos con SkiaSharp real, documentos cifrados con la llave
// de prueba, publicación con archivos, revisión, entrega pública con caché,
// BOLA, límites y fallo cerrado.
public sealed class MediaHttpTests
{
    private const string Landlord = "/api/v1/landlord";

    [Fact]
    public async Task Owner_uploads_photos_and_documents_and_only_the_owner_sees_them_before_review()
    {
        using var factory = new UserResourceFactory();
        var (alice, buildingId, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var _ = alice;
        using var carol = await factory.ClientForAsync(CarolId);

        using var photo = await alice.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File(Png(2400, 1600), "fachada.png"));
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        string photoId, privateUrl;
        using (var document = JsonDocument.Parse(await photo.Content.ReadAsStringAsync()))
        {
            var root = document.RootElement;
            Assert.Equal(new[] { "id", "url", "thumbUrl", "width", "height", "bytes", "createdAt" }, root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(1600, root.GetProperty("width").GetInt32());
            photoId = root.GetProperty("id").GetString()!;
            privateUrl = root.GetProperty("url").GetString()!;
            Assert.EndsWith("/private", privateUrl);
        }

        using var own = await alice.GetAsync(privateUrl + "?size=thumb");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("image/webp", own.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", own.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync(privateUrl)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/media/{photoId}")).StatusCode);

        for (var index = 0; index < 4; index++)
        {
            using var added = await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(800, 600), "cuarto.png"));
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        await AssertCodeAsync(await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(800, 600), "otra.png")), HttpStatusCode.Conflict, "photos_limit");
        var roomPhoto = factory.Media.Photos.Values.First(item => item.RoomId == roomId).Id;
        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"{Landlord}/rooms/{roomId}/photos/{roomPhoto}")).StatusCode);

        var pdf = "%PDF-1.4\n% INE de prueba\n"u8.ToArray();
        using var ine = await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/ine", File(pdf, "ine.pdf"));
        Assert.Equal(HttpStatusCode.OK, ine.StatusCode);
        using var proof = await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/property_proof", File(Png(300, 300), "predial.png"));
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        using var list = await alice.GetAsync($"{Landlord}/buildings/{buildingId}/documents");
        using (var document = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(new[] { "ine", "property_proof" }, items.Select(item => item.GetProperty("kind").GetString()));
            Assert.Equal(new[] { "id", "kind", "contentType", "bytes", "uploadedAt" }, items[0].EnumerateObject().Select(property => property.Name));
        }

        var stored = factory.Media.DocumentBytes.Values.First();
        Assert.False(stored.AsSpan().IndexOf("INE de prueba"u8) >= 0, "El documento se guardó sin cifrar.");
        Assert.Equal(HttpStatusCode.Forbidden, (await carol.GetAsync($"{Landlord}/buildings/{buildingId}/documents")).StatusCode);
        (await carol.PostAsJsonAsync($"{Landlord}/profile", new { fullName = "Carol Ruiz", phone = "+527731234568", rentalMode = "individual", acceptedTermsVersion = "2026-11" })).Dispose();
        Assert.Equal(HttpStatusCode.NotFound, (await carol.GetAsync($"{Landlord}/buildings/{buildingId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carol.DeleteAsync($"{Landlord}/buildings/{buildingId}/photo")).StatusCode);
    }

    [Fact]
    public async Task Publication_requires_files_and_reviewers_see_photos_and_documents()
    {
        using var factory = new UserResourceFactory(requirePublicationFiles: true);
        var (alice, buildingId, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var _ = alice;
        using var bob = await factory.ClientForAsync(BobId);

        using var blocked = await alice.PostAsync($"{Landlord}/buildings/{buildingId}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using (var steps = JsonDocument.Parse(await (await alice.GetAsync($"{Landlord}/buildings/{buildingId}/publication")).Content.ReadAsStringAsync()))
        {
            var ids = steps.RootElement.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("id").GetString()).ToArray();
            Assert.Contains("building_photo", ids);
            Assert.Contains("documents", ids);
        }

        var pdf = "%PDF-1.4\n% documento\n"u8.ToArray();
        (await alice.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File(Png(1200, 900), "f.png"))).Dispose();
        (await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(1200, 900), "c.png"))).Dispose();
        (await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/ine", File(pdf, "ine.pdf"))).Dispose();
        (await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/property_proof", File(pdf, "predial.pdf"))).Dispose();
        Assert.Equal(HttpStatusCode.Accepted, (await alice.PostAsync($"{Landlord}/buildings/{buildingId}/publish", null)).StatusCode);
        await AssertCodeAsync(await alice.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File(Png(400, 300), "x.png")), HttpStatusCode.Conflict, "building_in_review");

        await factory.Moderation.GrantAsync(BobId, StaffRoles.Reviewer, "test", DateTimeOffset.UtcNow, CancellationToken.None);
        string documentId, photoUrl;
        using (var detail = JsonDocument.Parse(await (await bob.GetAsync($"/api/v1/admin/buildings/{buildingId}")).Content.ReadAsStringAsync()))
        {
            var root = detail.RootElement;
            photoUrl = root.GetProperty("building").GetProperty("photo").GetProperty("url").GetString()!;
            Assert.Single(root.GetProperty("rooms")[0].GetProperty("photos").EnumerateArray());
            var documents = root.GetProperty("documents").EnumerateArray().ToArray();
            Assert.Equal(2, documents.Length);
            documentId = documents[0].GetProperty("id").GetString()!;
        }

        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync(photoUrl)).StatusCode);
        using var read = await bob.GetAsync($"/api/v1/admin/buildings/{buildingId}/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(pdf, await read.Content.ReadAsByteArrayAsync());
        Assert.Contains("no-store", read.Headers.CacheControl?.ToString());
        Assert.Contains(factory.Moderation.Audit, entry => entry.Action == "document.viewed");
        await AssertCodeAsync(await alice.GetAsync($"/api/v1/admin/buildings/{buildingId}/documents/{documentId}"), HttpStatusCode.Forbidden, "role_required");

        var version = factory.Buildings.Buildings[buildingId].Version;
        Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync($"/api/v1/admin/buildings/{buildingId}/approve", new { version })).StatusCode);
        Assert.All(factory.Media.Documents.Values, document => Assert.NotNull(document.RetentionUntil));

        var photoId = factory.Media.Photos.Values.Single(item => item.RoomId is null).Id;
        using var anonymous = factory.CreateClient();
        using var publicPhoto = await anonymous.GetAsync($"/api/v1/media/{photoId}?size=thumb");
        Assert.Equal(HttpStatusCode.OK, publicPhoto.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", publicPhoto.Headers.CacheControl?.ToString());
        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/media/{photoId}?size=thumb");
        again.Headers.IfNoneMatch.Add(publicPhoto.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await anonymous.SendAsync(again)).StatusCode);

        using var search = JsonDocument.Parse(await (await alice.GetAsync($"/api/v1/rooms/{roomId}")).Content.ReadAsStringAsync());
        Assert.StartsWith("/api/v1/media/", search.RootElement.GetProperty("thumbnailUrl").GetString());
        Assert.DoesNotContain("/private", search.RootElement.GetProperty("photos")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Files_are_validated_by_content_size_and_type()
    {
        using var factory = new UserResourceFactory();
        var (alice, buildingId, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var _ = alice;

        await AssertCodeAsync(await alice.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File("<svg onload=alert(1)>"u8.ToArray(), "foto.png")), HttpStatusCode.UnsupportedMediaType, "unsupported_media");
        await AssertCodeAsync(await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/ine", File("MZ ejecutable"u8.ToArray(), "ine.pdf")), HttpStatusCode.UnsupportedMediaType, "unsupported_media");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PutAsync($"{Landlord}/buildings/{buildingId}/documents/curp", File("%PDF-1.4"u8.ToArray(), "x.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await alice.PutAsJsonAsync($"{Landlord}/buildings/{buildingId}/photo", new { file = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await alice.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(new byte[(5 * 1024 * 1024) + 1], "grande.png"))).StatusCode);

        using var carol = await factory.ClientForAsync(CarolId);
        Assert.Equal(HttpStatusCode.Forbidden, (await carol.PutAsync($"{Landlord}/buildings/{buildingId}/photo", File(Png(100, 100), "a.png"))).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_room_removes_its_photos_and_the_store_fails_closed()
    {
        using var factory = new UserResourceFactory();
        var (alice, _, roomId) = await DraftBuildingOfAliceAsync(factory);
        using var client = alice;
        (await client.PostAsync($"{Landlord}/rooms/{roomId}/photos", File(Png(640, 480), "c.png"))).Dispose();
        Assert.Single(factory.Media.Photos);

        var version = factory.Rooms.Rooms[roomId].Version;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Landlord}/rooms/{roomId}?version={version}")).StatusCode);
        Assert.Empty(factory.Media.Photos);

        factory.Media.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await factory.CreateClient().GetAsync("/api/v1/media/6a0000000000000000000001")).StatusCode);
    }

    // --- Apoyo -------------------------------------------------------

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(30, 120, 200));
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

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        }
    }

    // Alice registra un edificio en borrador con un cuarto listo (NV-12).
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
