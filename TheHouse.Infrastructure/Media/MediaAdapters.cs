using System.Security.Cryptography;
using SkiaSharp;
using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;

namespace TheHouse.Infrastructure.Media;

// NV-13: re-codificación con SkiaSharp 4.154.0 (versión fijada, M-BE-073).
// Lee primero solo el encabezado para rechazar imágenes de más de 40
// megapíxeles antes de decodificar; aplica la orientación EXIF y vuelve a
// codificar a WebP, que no lleva EXIF, GPS ni ningún otro metadato.
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int FullSide = 1600;

    public const int ThumbSide = 400;

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public ProcessedImage Process(ReadOnlyMemory<byte> input)
    {
        using var data = SKData.CreateCopy(input.Span);
        using var codec = SKCodec.Create(data) ?? throw new MediaRejectedException("unsupported_media");
        if ((long)codec.Info.Width * codec.Info.Height > MediaRules.MaximumMegapixels * 1_000_000L)
        {
            throw new MediaRejectedException("image_too_large");
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new MediaRejectedException("unsupported_media");
        }

        using var oriented = Orient(decoded, codec.EncodedOrigin);
        using var full = Fit(oriented, FullSide);
        using var thumb = Fit(full, ThumbSide);
        return new ProcessedImage(Encode(full, 80), Encode(thumb, 70), full.Width, full.Height);
    }

    // Copia ajustada para que el lado mayor no pase de `side`.
    private static SKBitmap Fit(SKBitmap source, int side)
    {
        var scale = Math.Min(1.0, side / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), Sampling)
            ?? throw new MediaRejectedException("unsupported_media");
    }

    private static byte[] Encode(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality) ?? throw new MediaRejectedException("unsupported_media");
        return encoded.ToArray();
    }

    // Aplica la orientación de la cámara para que la foto se vea derecha.
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1, width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, width / 2f, height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1, 0, height / 2f);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1, 0, source.Height / 2f);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                canvas.Scale(1, -1, 0, source.Height / 2f);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return target;
    }
}

// NV-13: AES-256-GCM. Cada documento usa una llave de datos aleatoria; la
// llave de datos se guarda cifrada con la llave maestra (secreto de Container
// Apps, M-BE-038). Sin llave maestra los documentos no están disponibles (503).
public sealed class AesGcmDocumentCipher(byte[]? masterKey) : IDocumentCipher
{
    private const int KeySize = 32;

    private const int NonceSize = 12;

    private const int TagSize = 16;

    public EncryptedDocument Encrypt(ReadOnlySpan<byte> plain)
    {
        var master = Master();
        var dataKey = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var content = new byte[plain.Length + TagSize];
            using (var aes = new AesGcm(dataKey, TagSize))
            {
                aes.Encrypt(nonce, plain, content.AsSpan(0, plain.Length), content.AsSpan(plain.Length));
            }

            var keyNonce = RandomNumberGenerator.GetBytes(NonceSize);
            var wrapped = new byte[NonceSize + KeySize + TagSize];
            keyNonce.CopyTo(wrapped, 0);
            using (var aes = new AesGcm(master, TagSize))
            {
                aes.Encrypt(keyNonce, dataKey, wrapped.AsSpan(NonceSize, KeySize), wrapped.AsSpan(NonceSize + KeySize));
            }

            return new EncryptedDocument(content, nonce, wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public byte[] Decrypt(EncryptedDocument document)
    {
        var master = Master();
        var dataKey = new byte[KeySize];
        try
        {
            var wrapped = document.WrappedKey.AsSpan();
            using (var aes = new AesGcm(master, TagSize))
            {
                aes.Decrypt(wrapped[..NonceSize], wrapped.Slice(NonceSize, KeySize), wrapped[(NonceSize + KeySize)..], dataKey);
            }

            var length = document.Content.Length - TagSize;
            var plain = new byte[length];
            using (var aes = new AesGcm(dataKey, TagSize))
            {
                aes.Decrypt(document.Nonce, document.Content.AsSpan(0, length), document.Content.AsSpan(length), plain);
            }

            return plain;
        }
        catch (CryptographicException)
        {
            throw new DependencyUnavailableException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    // La llave maestra viene en base64 (32 bytes); nunca se registra.
    public static byte[]? ParseMasterKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var key = Convert.FromBase64String(base64.Trim());
            return key.Length == KeySize ? key : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private byte[] Master() => masterKey is { Length: KeySize } key ? key : throw new DependencyUnavailableException();
}
