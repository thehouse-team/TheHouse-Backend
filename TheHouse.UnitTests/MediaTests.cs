using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-13: firma de bytes, rutas de las fotos, resumen para NV-12/NV-14/NV-21,
// pasos de publicación y retención de documentos.
public sealed class MediaTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 16, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] WebP = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBPVP8 "u8.ToArray()];
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8.ToArray()];

    [Fact]
    public void Types_are_detected_by_byte_signature_not_by_name()
    {
        Assert.True(MediaRules.IsImage(Jpeg));
        Assert.True(MediaRules.IsImage(Png));
        Assert.True(MediaRules.IsImage(WebP));
        Assert.False(MediaRules.IsImage(Pdf));
        Assert.False(MediaRules.IsImage("<svg onload=alert(1)>"u8));
        Assert.False(MediaRules.IsImage("GIF89a"u8));

        Assert.Equal("application/pdf", MediaRules.DocumentType(Pdf));
        Assert.Equal("image/jpeg", MediaRules.DocumentType(Jpeg));
        Assert.Equal("image/png", MediaRules.DocumentType(Png));
        Assert.Null(MediaRules.DocumentType(WebP));
        Assert.Null(MediaRules.DocumentType("MZ\u0090"u8));
        Assert.Null(MediaRules.DocumentType("<html>"u8));
        Assert.Equal(64, MediaRules.Sha256(Pdf).Length);
    }

    [Fact]
    public void Photo_urls_are_public_only_when_asked()
    {
        var photo = Photo("6a0000000000000000000001", "b1", null);

        var visible = MediaRules.ToResponse(photo, publicUrl: true);
        var hidden = MediaRules.ToResponse(photo, publicUrl: false);

        Assert.Equal("/api/v1/media/6a0000000000000000000001", visible.Url);
        Assert.Equal("/api/v1/media/6a0000000000000000000001?size=thumb", visible.ThumbUrl);
        Assert.Equal("/api/v1/media/6a0000000000000000000001/private", hidden.Url);
        Assert.Equal("/api/v1/media/6a0000000000000000000001/private?size=thumb", hidden.ThumbUrl);
    }

    [Fact]
    public async Task Snapshot_groups_building_and_room_photos()
    {
        var store = new InMemoryMediaRepository();
        await store.AddPhotoAsync(Photo("6a0000000000000000000001", "b1", null), [1], [2], CancellationToken.None);
        await store.AddPhotoAsync(Photo("6a0000000000000000000002", "b1", "r1"), [1], [2], CancellationToken.None);
        await store.AddPhotoAsync(Photo("6a0000000000000000000003", "b1", "r1", 1), [1], [2], CancellationToken.None);
        await store.AddPhotoAsync(Photo("6a0000000000000000000004", "b2", null), [1], [2], CancellationToken.None);
        var port = new MediaPort(store, new ManualTimeProvider(Now));

        var snapshot = await port.SnapshotAsync(["b1"], publicUrls: true, CancellationToken.None);

        Assert.Equal("6a0000000000000000000001", snapshot.Building("b1")!.Id);
        Assert.Null(snapshot.Building("b2"));
        Assert.Equal(new[] { "6a0000000000000000000002", "6a0000000000000000000003" }, snapshot.Room("r1").Select(photo => photo.Id));
        Assert.Empty(snapshot.Room("r2"));
        Assert.Same(Application.Abstractions.Media.MediaSnapshot.Empty, await port.SnapshotAsync([], true, CancellationToken.None));
    }

    [Fact]
    public async Task Publication_needs_the_building_photo_and_both_documents()
    {
        var store = new InMemoryMediaRepository();
        var port = new MediaPort(store, new ManualTimeProvider(Now));
        Assert.True(port.PublicationRequiresFiles);
        Assert.False(new MediaPort(store, new ManualTimeProvider(Now), new MediaOptions { RequirePublicationFiles = false }).PublicationRequiresFiles);
        Assert.Equal((false, false), await port.PublicationAsync("b1", CancellationToken.None));

        await store.AddPhotoAsync(Photo("6a0000000000000000000005", "b1", "r1"), [1], [2], CancellationToken.None);
        await store.SaveDocumentAsync(Document("6a0000000000000000000006", "b1", DocumentKinds.Ine), [9], CancellationToken.None);
        Assert.Equal((false, false), await port.PublicationAsync("b1", CancellationToken.None));

        await store.AddPhotoAsync(Photo("6a0000000000000000000007", "b1", null), [1], [2], CancellationToken.None);
        await store.SaveDocumentAsync(Document("6a0000000000000000000008", "b1", DocumentKinds.PropertyProof), [9], CancellationToken.None);
        Assert.Equal((true, true), await port.PublicationAsync("b1", CancellationToken.None));
    }

    [Fact]
    public async Task Documents_expire_90_days_after_the_review_decision()
    {
        var store = new InMemoryMediaRepository();
        var clock = new ManualTimeProvider(Now);
        var port = new MediaPort(store, clock);
        await store.SaveDocumentAsync(Document("6a0000000000000000000009", "b1", DocumentKinds.Ine), [9], CancellationToken.None);
        await store.SaveDocumentAsync(Document("6a000000000000000000000a", "b2", DocumentKinds.Ine), [9], CancellationToken.None);

        await port.StartRetentionAsync("b1", Now, CancellationToken.None);
        clock.Now = Now.AddDays(89);
        Assert.Equal(0, await port.PurgeExpiredDocumentsAsync(CancellationToken.None));
        clock.Now = Now.AddDays(90);
        Assert.Equal(1, await port.PurgeExpiredDocumentsAsync(CancellationToken.None));

        Assert.Equal("b2", Assert.Single(store.Documents.Values).BuildingId);
    }

    [Fact]
    public async Task Replacing_a_document_keeps_one_per_kind_and_deleting_a_building_removes_its_files()
    {
        var store = new InMemoryMediaRepository();
        await store.SaveDocumentAsync(Document("6a000000000000000000000b", "b1", DocumentKinds.Ine), [1], CancellationToken.None);
        await store.SaveDocumentAsync(Document("6a000000000000000000000c", "b1", DocumentKinds.Ine), [2], CancellationToken.None);
        await store.AddPhotoAsync(Photo("6a000000000000000000000d", "b1", "r1"), [1], [2], CancellationToken.None);

        Assert.Equal("6a000000000000000000000c", Assert.Single(store.Documents.Values).Id);
        await new MediaPort(store, new ManualTimeProvider(Now)).DeleteRoomAsync("r1", CancellationToken.None);
        Assert.Empty(store.Photos);
        await new MediaPort(store, new ManualTimeProvider(Now)).DeleteBuildingAsync("b1", CancellationToken.None);
        Assert.Empty(store.Documents);
    }

    private static MediaPhoto Photo(string id, string buildingId, string? roomId, int minutes = 0) =>
        new() { Id = id, OwnerId = "owner", BuildingId = buildingId, RoomId = roomId, Width = 1600, Height = 1200, Bytes = 1100, CreatedAt = Now.AddMinutes(minutes) };

    private static VerificationDocument Document(string id, string buildingId, string kind) =>
        new() { Id = id, OwnerId = "owner", BuildingId = buildingId, Kind = kind, ContentType = "application/pdf", Bytes = 1, UploadedAt = Now };
}
