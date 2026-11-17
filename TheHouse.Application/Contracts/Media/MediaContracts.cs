namespace TheHouse.Application.Contracts.Media;

// NV-13 §6.1: foto con rutas relativas a la API; antes de la verificación
// del edificio apuntan a `/private`.
public sealed record PhotoResponse(
    string Id,
    string Url,
    string ThumbUrl,
    int Width,
    int Height,
    long Bytes,
    DateTimeOffset CreatedAt);

// Solo metadatos: el contenido nunca sale al dueño ni al público.
public sealed record DocumentResponse(string Id, string Kind, string ContentType, long Bytes, DateTimeOffset UploadedAt);

public sealed record DocumentListResponse(IReadOnlyList<DocumentResponse> Items);

// Archivo leído de una carga multipart, ya acotado en tamaño.
public sealed record UploadedFile(byte[] Content);
