using TheHouse.Application.Contracts.Media;

namespace TheHouse.Application.Abstractions.Media;

// NV-13: lo que Listings (NV-12), Search (NV-14) y Moderation (NV-21) usan del
// módulo Media, sin tocar sus colecciones.
public interface IMediaPort
{
    // false solo en pruebas que no cargan archivos (`Media:RequirePublicationFiles`).
    bool PublicationRequiresFiles { get; }

    // Foto del edificio y fotos de cada cuarto, para armar respuestas.
    Task<MediaSnapshot> SnapshotAsync(IReadOnlyCollection<string> buildingIds, bool publicUrls, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentResponse>> DocumentsAsync(string buildingId, CancellationToken cancellationToken);

    // Pasos de publicación: foto del edificio y los dos documentos.
    Task<(bool Photo, bool Documents)> PublicationAsync(string buildingId, CancellationToken cancellationToken);

    Task StartRetentionAsync(string buildingId, DateTimeOffset decidedAt, CancellationToken cancellationToken);

    Task DeleteBuildingAsync(string buildingId, CancellationToken cancellationToken);

    Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken);
}

public sealed record MediaSnapshot(
    IReadOnlyDictionary<string, PhotoResponse> BuildingPhotos,
    IReadOnlyDictionary<string, IReadOnlyList<PhotoResponse>> RoomPhotos)
{
    public static readonly MediaSnapshot Empty = new(
        new Dictionary<string, PhotoResponse>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<PhotoResponse>>(StringComparer.Ordinal));

    public PhotoResponse? Building(string buildingId) => BuildingPhotos.GetValueOrDefault(buildingId);

    public IReadOnlyList<PhotoResponse> Room(string roomId) =>
        RoomPhotos.TryGetValue(roomId, out var photos) ? photos : Array.Empty<PhotoResponse>();
}
