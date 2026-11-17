using System.Security.Cryptography;
using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Media;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-13: opciones del módulo (sección `Media`).
public sealed class MediaOptions
{
    public bool RequirePublicationFiles { get; init; } = true;
}

// NV-13: límites y tipos permitidos (firma de bytes, no extensión ni
// `Content-Type`).
public static class MediaRules
{
    public const int MaximumPhotoBytes = 5 * 1024 * 1024;

    public const int MaximumDocumentBytes = 10 * 1024 * 1024;

    public const int MaximumRoomPhotos = 4;

    public const long OwnerQuotaBytes = 60L * 1024 * 1024;

    public const int MaximumMegapixels = 40;

    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public static bool IsImage(ReadOnlySpan<byte> bytes) => Jpeg(bytes) || Png(bytes) || WebP(bytes);

    // PDF, JPEG o PNG; devuelve el tipo detectado o null.
    public static string? DocumentType(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith("%PDF-"u8) ? "application/pdf"
        : Jpeg(bytes) ? "image/jpeg"
        : Png(bytes) ? "image/png"
        : null;

    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string PublicUrl(string id) => "/api/v1/media/" + id;

    public static string PrivateUrl(string id) => "/api/v1/media/" + id + "/private";

    public static PhotoResponse ToResponse(MediaPhoto photo, bool publicUrl)
    {
        var url = publicUrl ? PublicUrl(photo.Id) : PrivateUrl(photo.Id);
        return new PhotoResponse(photo.Id, url, url + "?size=thumb", photo.Width, photo.Height, photo.Bytes, photo.CreatedAt);
    }

    private static bool Jpeg(ReadOnlySpan<byte> bytes) => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool Png(ReadOnlySpan<byte> bytes) => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    private static bool WebP(ReadOnlySpan<byte> bytes) => bytes.Length > 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8);
}

// NV-13 (RF-76, RF-77, RNF-19): fotos de edificios y cuartos y documentos de
// verificación. Lo ajeno responde 404; los documentos solo los lee el equipo
// de revisión (NV-21) y cada lectura queda en la bitácora.
public sealed class MediaService(
    IMediaRepository media,
    IImageProcessor images,
    IDocumentCipher cipher,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    ILandlordProfileRepository landlords,
    StaffService staff,
    TimeProvider timeProvider)
{
    public async Task<PhotoResponse> SetBuildingPhotoAsync(string ownerId, string? buildingId, UploadedFile file, CancellationToken cancellationToken)
    {
        var building = await EditableBuildingAsync(ownerId, buildingId, cancellationToken);
        var previous = (await media.ListPhotosByBuildingsAsync([building.Id], cancellationToken)).FirstOrDefault(photo => photo.RoomId is null);
        var photo = await StorePhotoAsync(ownerId, building, null, file, previous?.Bytes ?? 0, cancellationToken);
        if (previous is not null)
        {
            await media.DeletePhotoAsync(previous.Id, cancellationToken);
        }

        return MediaRules.ToResponse(photo, building.Status == BuildingStatus.Verified);
    }

    public async Task DeleteBuildingPhotoAsync(string ownerId, string? buildingId, CancellationToken cancellationToken)
    {
        var building = await EditableBuildingAsync(ownerId, buildingId, cancellationToken);
        var photo = (await media.ListPhotosByBuildingsAsync([building.Id], cancellationToken)).FirstOrDefault(item => item.RoomId is null)
            ?? throw new NotFoundException("photo");
        await media.DeletePhotoAsync(photo.Id, cancellationToken);
    }

    public async Task<PhotoResponse> AddRoomPhotoAsync(string ownerId, string? roomId, UploadedFile file, CancellationToken cancellationToken)
    {
        var (room, building) = await EditableRoomAsync(ownerId, roomId, cancellationToken);
        var existing = (await media.ListPhotosByBuildingsAsync([building.Id], cancellationToken)).Count(photo => photo.RoomId == room.Id);
        if (existing >= MediaRules.MaximumRoomPhotos)
        {
            throw new ConflictException("photos_limit");
        }

        var photo = await StorePhotoAsync(ownerId, building, room.Id, file, 0, cancellationToken);
        return MediaRules.ToResponse(photo, building.Status == BuildingStatus.Verified);
    }

    public async Task DeleteRoomPhotoAsync(string ownerId, string? roomId, string? photoId, CancellationToken cancellationToken)
    {
        var (room, _) = await EditableRoomAsync(ownerId, roomId, cancellationToken);
        MessagingRules.EnsureId(photoId, "photoId");
        var photo = await media.GetPhotoAsync(photoId!.ToLowerInvariant(), cancellationToken);
        if (photo is null || photo.RoomId != room.Id)
        {
            throw new NotFoundException("photo");
        }

        await media.DeletePhotoAsync(photo.Id, cancellationToken);
    }

    public async Task<DocumentResponse> UploadDocumentAsync(string ownerId, string? buildingId, string? kind, UploadedFile file, CancellationToken cancellationToken)
    {
        if (kind is null || !DocumentKinds.All.Contains(kind))
        {
            throw new RequestValidationException("kind", "kind must be ine or property_proof.");
        }

        var contentType = MediaRules.DocumentType(file.Content) ?? throw new MediaRejectedException("unsupported_media");
        var building = await EditableBuildingAsync(ownerId, buildingId, cancellationToken);
        var replaced = (await media.ListDocumentsAsync(building.Id, cancellationToken)).FirstOrDefault(item => item.Kind == kind);
        await EnsureQuotaAsync(ownerId, file.Content.Length, replaced?.Bytes ?? 0, cancellationToken);

        var encrypted = cipher.Encrypt(file.Content);
        var now = timeProvider.GetUtcNow();
        var saved = await media.SaveDocumentAsync(
            new VerificationDocument
            {
                Id = MessagingIds.New(now),
                OwnerId = ownerId,
                BuildingId = building.Id,
                Kind = kind,
                ContentType = contentType,
                Bytes = file.Content.Length,
                Sha256 = MediaRules.Sha256(file.Content),
                Nonce = encrypted.Nonce,
                WrappedKey = encrypted.WrappedKey,
                UploadedAt = now,
            },
            encrypted.Content,
            cancellationToken);
        return ToResponse(saved);
    }

    public async Task<DocumentListResponse> ListDocumentsAsync(string ownerId, string? buildingId, CancellationToken cancellationToken)
    {
        var building = await OwnedBuildingAsync(ownerId, buildingId, cancellationToken);
        var items = await media.ListDocumentsAsync(building.Id, cancellationToken);
        return new DocumentListResponse(items.OrderBy(item => item.Kind, StringComparer.Ordinal).Select(ToResponse).ToArray());
    }

    // Ruta pública: solo fotos de edificios verificados y de cuartos que existen.
    public async Task<byte[]> PublicPhotoAsync(string? id, bool thumb, CancellationToken cancellationToken)
    {
        var photo = await LoadPhotoAsync(id, cancellationToken);
        var building = await buildings.GetAsync(photo.BuildingId, cancellationToken);
        if (building?.Status != BuildingStatus.Verified ||
            (photo.RoomId is not null && await rooms.GetAsync(photo.RoomId, cancellationToken) is null))
        {
            throw new NotFoundException("photo");
        }

        return await media.ReadPhotoAsync(photo.Id, thumb, cancellationToken) ?? throw new NotFoundException("photo");
    }

    // Ruta privada: el dueño o el equipo de revisión, en cualquier estado.
    public async Task<byte[]> PrivatePhotoAsync(string userId, string? id, bool thumb, CancellationToken cancellationToken)
    {
        var photo = await LoadPhotoAsync(id, cancellationToken);
        if (photo.OwnerId != userId)
        {
            try
            {
                await staff.RequireAsync(userId, StaffRoles.Reviewer, cancellationToken);
            }
            catch (ForbiddenException)
            {
                throw new NotFoundException("photo");
            }
        }

        return await media.ReadPhotoAsync(photo.Id, thumb, cancellationToken) ?? throw new NotFoundException("photo");
    }

    // Documento descifrado para el revisor; cada lectura queda en la bitácora.
    public async Task<(byte[] Content, string ContentType, string Kind)> ReviewerDocumentAsync(
        string userId,
        string? buildingId,
        string? documentId,
        CancellationToken cancellationToken)
    {
        await staff.RequireAsync(userId, StaffRoles.Reviewer, cancellationToken);
        MessagingRules.EnsureId(buildingId, "id");
        MessagingRules.EnsureId(documentId, "documentId");
        var document = await media.GetDocumentAsync(documentId!.ToLowerInvariant(), cancellationToken);
        if (document is null || document.BuildingId != buildingId!.ToLowerInvariant())
        {
            throw new NotFoundException("document");
        }

        var encrypted = await media.ReadDocumentAsync(document.Id, cancellationToken) ?? throw new NotFoundException("document");
        var plain = cipher.Decrypt(new EncryptedDocument(encrypted, document.Nonce, document.WrappedKey));
        await staff.AuditAsync(userId, "document.viewed", "building", document.BuildingId, document.Kind, cancellationToken);
        return (plain, document.ContentType, document.Kind);
    }

    private async Task<MediaPhoto> StorePhotoAsync(string ownerId, Building building, string? roomId, UploadedFile file, long replacedBytes, CancellationToken cancellationToken)
    {
        if (!MediaRules.IsImage(file.Content))
        {
            throw new MediaRejectedException("unsupported_media");
        }

        var processed = images.Process(file.Content);
        var bytes = (long)processed.Full.Length + processed.Thumb.Length;
        await EnsureQuotaAsync(ownerId, bytes, replacedBytes, cancellationToken);
        var now = timeProvider.GetUtcNow();
        return await media.AddPhotoAsync(
            new MediaPhoto
            {
                Id = MessagingIds.New(now),
                OwnerId = ownerId,
                BuildingId = building.Id,
                RoomId = roomId,
                Width = processed.Width,
                Height = processed.Height,
                Bytes = bytes,
                Sha256 = MediaRules.Sha256(processed.Full),
                CreatedAt = now,
            },
            processed.Full,
            processed.Thumb,
            cancellationToken);
    }

    private async Task EnsureQuotaAsync(string ownerId, long adding, long replacing, CancellationToken cancellationToken)
    {
        if (await media.UsageAsync(ownerId, cancellationToken) - replacing + adding > MediaRules.OwnerQuotaBytes)
        {
            throw new ConflictException("storage_limit");
        }
    }

    private async Task<MediaPhoto> LoadPhotoAsync(string? id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        return await media.GetPhotoAsync(id!.ToLowerInvariant(), cancellationToken) ?? throw new NotFoundException("photo");
    }

    private async Task<Building> OwnedBuildingAsync(string ownerId, string? buildingId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(ownerId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }

        MessagingRules.EnsureId(buildingId, "id");
        var building = await buildings.GetAsync(buildingId!.ToLowerInvariant(), cancellationToken);
        return building is not null && building.OwnerId == ownerId ? building : throw new NotFoundException("building");
    }

    // Solo con el edificio en `draft` o `verified` (como NV-12).
    private async Task<Building> EditableBuildingAsync(string ownerId, string? buildingId, CancellationToken cancellationToken) =>
        (await OwnedBuildingAsync(ownerId, buildingId, cancellationToken)).EnsureEditable();

    private async Task<(Room Room, Building Building)> EditableRoomAsync(string ownerId, string? roomId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(ownerId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }

        MessagingRules.EnsureId(roomId, "id");
        var room = await rooms.GetAsync(roomId!.ToLowerInvariant(), cancellationToken);
        if (room is null || room.OwnerId != ownerId)
        {
            throw new NotFoundException("room");
        }

        var building = await buildings.GetAsync(room.BuildingId, cancellationToken) ?? throw new NotFoundException("room");
        return (room, building.EnsureEditable());
    }

    private static DocumentResponse ToResponse(VerificationDocument document) =>
        new(document.Id, document.Kind, document.ContentType, document.Bytes, document.UploadedAt);
}

// NV-13: puerto para NV-12, NV-14 y NV-21.
public sealed class MediaPort(IMediaRepository media, TimeProvider timeProvider, MediaOptions? options = null) : IMediaPort
{
    public bool PublicationRequiresFiles => options?.RequirePublicationFiles ?? true;

    public async Task<MediaSnapshot> SnapshotAsync(IReadOnlyCollection<string> buildingIds, bool publicUrls, CancellationToken cancellationToken)
    {
        if (buildingIds.Count == 0)
        {
            return MediaSnapshot.Empty;
        }

        var photos = await media.ListPhotosByBuildingsAsync(buildingIds, cancellationToken);
        var building = photos.Where(photo => photo.RoomId is null)
            .GroupBy(photo => photo.BuildingId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => MediaRules.ToResponse(group.Last(), publicUrls), StringComparer.Ordinal);
        var room = photos.Where(photo => photo.RoomId is not null)
            .GroupBy(photo => photo.RoomId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PhotoResponse>)group.Select(photo => MediaRules.ToResponse(photo, publicUrls)).ToArray(), StringComparer.Ordinal);
        return new MediaSnapshot(building, room);
    }

    public async Task<IReadOnlyList<DocumentResponse>> DocumentsAsync(string buildingId, CancellationToken cancellationToken) =>
        (await media.ListDocumentsAsync(buildingId, cancellationToken))
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .Select(item => new DocumentResponse(item.Id, item.Kind, item.ContentType, item.Bytes, item.UploadedAt))
            .ToArray();

    public async Task<(bool Photo, bool Documents)> PublicationAsync(string buildingId, CancellationToken cancellationToken)
    {
        var photo = (await media.ListPhotosByBuildingsAsync([buildingId], cancellationToken)).Any(item => item.RoomId is null);
        var kinds = (await media.ListDocumentsAsync(buildingId, cancellationToken)).Select(item => item.Kind).ToHashSet(StringComparer.Ordinal);
        return (photo, DocumentKinds.All.All(kinds.Contains));
    }

    public Task StartRetentionAsync(string buildingId, DateTimeOffset decidedAt, CancellationToken cancellationToken) =>
        media.SetRetentionAsync(buildingId, decidedAt + MediaRules.Retention, cancellationToken);

    public Task DeleteBuildingAsync(string buildingId, CancellationToken cancellationToken) => media.DeleteByBuildingAsync(buildingId, cancellationToken);

    public Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken) => media.DeleteByRoomAsync(roomId, cancellationToken);

    // Limpieza periódica de documentos vencidos.
    public Task<int> PurgeExpiredDocumentsAsync(CancellationToken cancellationToken) =>
        media.DeleteExpiredDocumentsAsync(timeProvider.GetUtcNow(), cancellationToken);
}
