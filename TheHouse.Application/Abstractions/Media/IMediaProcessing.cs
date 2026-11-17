namespace TheHouse.Application.Abstractions.Media;

// NV-13: re-codificación de fotos (implementada con ImageSharp en
// Infrastructure). Lanza `MediaRejectedException` si la imagen no se puede
// decodificar o supera los megapíxeles permitidos.
public interface IImageProcessor
{
    ProcessedImage Process(ReadOnlyMemory<byte> input);
}

// WebP de máximo 1600 px y miniatura de 400 px, sin metadatos.
public sealed record ProcessedImage(byte[] Full, byte[] Thumb, int Width, int Height);

// NV-13: cifrado de documentos (AES-256-GCM con llave de datos por documento
// envuelta por la llave maestra). Lanza `DependencyUnavailableException` si
// la llave maestra no está configurada.
public interface IDocumentCipher
{
    EncryptedDocument Encrypt(ReadOnlySpan<byte> plain);

    byte[] Decrypt(EncryptedDocument document);
}

// `Content` lleva el texto cifrado seguido de la etiqueta de autenticación.
public sealed record EncryptedDocument(byte[] Content, byte[] Nonce, byte[] WrappedKey);

public sealed class MediaRejectedException(string code) : Exception("The media file was rejected.")
{
    public string Code { get; } = code;
}
