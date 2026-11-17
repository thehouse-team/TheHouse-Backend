namespace TheHouse.Domain.Entities;

// NV-13: foto re-codificada de un edificio (`RoomId` null) o de un cuarto.
// Los bytes viven en GridFS; aquí solo los metadatos.
public sealed record MediaPhoto
{
    public string Id { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string? RoomId { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    // Bytes de la foto más su miniatura (cuentan para la cuota).
    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

public static class DocumentKinds
{
    public const string Ine = "ine";

    public const string PropertyProof = "property_proof";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Ine, PropertyProof };
}

// NV-13: documento de verificación cifrado (AES-256-GCM). `Sha256` es del
// contenido sin cifrar; `WrappedKey` es la llave de datos cifrada con la
// llave maestra, que nunca se guarda en la base.
public sealed record VerificationDocument
{
    public string Id { get; init; } = string.Empty;

    public string OwnerId { get; init; } = string.Empty;

    public string BuildingId { get; init; } = string.Empty;

    public string Kind { get; init; } = DocumentKinds.Ine;

    public string ContentType { get; init; } = string.Empty;

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = string.Empty;

    public byte[] Nonce { get; init; } = [];

    public byte[] WrappedKey { get; init; } = [];

    public DateTimeOffset UploadedAt { get; init; }

    public DateTimeOffset? RetentionUntil { get; init; }
}
