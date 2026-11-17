using System.IO.Compression;
using System.Security.Cryptography;
using SkiaSharp;
using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Common;
using TheHouse.Http;
using TheHouse.Infrastructure.Media;

namespace TheHouse.SecurityTests;

// NV-13 (RNF-19): documentos cifrados con AES-256-GCM (manipulación detectada
// y sin llave → no disponible), fotos sin EXIF ni GPS tras re-codificar, límite
// de megapíxeles antes de decodificar y códigos con mensaje en español.
public sealed class MediaSecurityTests
{
    private static readonly byte[] MasterKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Documents_round_trip_and_tampering_is_detected()
    {
        var cipher = new AesGcmDocumentCipher(MasterKey);
        var plain = "%PDF-1.4 INE confidencial"u8.ToArray();

        var encrypted = cipher.Encrypt(plain);

        Assert.Equal(plain, cipher.Decrypt(encrypted));
        Assert.False(encrypted.Content.AsSpan().IndexOf("confidencial"u8) >= 0);
        Assert.NotEqual(encrypted.WrappedKey, cipher.Encrypt(plain).WrappedKey);
        var tampered = encrypted.Content.ToArray();
        tampered[3] ^= 0x01;
        Assert.Throws<DependencyUnavailableException>(() => cipher.Decrypt(encrypted with { Content = tampered }));
        Assert.Throws<DependencyUnavailableException>(() => new AesGcmDocumentCipher(RandomNumberGenerator.GetBytes(32)).Decrypt(encrypted));
    }

    [Fact]
    public void Without_a_valid_master_key_documents_are_unavailable()
    {
        Assert.Null(AesGcmDocumentCipher.ParseMasterKey(null));
        Assert.Null(AesGcmDocumentCipher.ParseMasterKey("no-es-base64"));
        Assert.Null(AesGcmDocumentCipher.ParseMasterKey(Convert.ToBase64String(new byte[16])));
        Assert.Throws<DependencyUnavailableException>(() => new AesGcmDocumentCipher(null).Encrypt([1, 2, 3]));
    }

    [Fact]
    public void Photos_are_reencoded_to_webp_without_exif_or_gps()
    {
        var jpeg = WithExif(Jpeg(2000, 1000), "GPSSECRET 19.4326,-99.1332");
        Assert.True(jpeg.AsSpan().IndexOf("GPSSECRET"u8) >= 0);

        var processed = new SkiaImageProcessor().Process(jpeg);

        Assert.Equal(1600, processed.Width);
        Assert.Equal(800, processed.Height);
        foreach (var output in new[] { processed.Full, processed.Thumb })
        {
            Assert.True(output.AsSpan().StartsWith("RIFF"u8) && output.AsSpan(8, 4).SequenceEqual("WEBP"u8));
            Assert.False(output.AsSpan().IndexOf("GPSSECRET"u8) >= 0);
            Assert.False(output.AsSpan().IndexOf("Exif"u8) >= 0);
        }
    }

    [Fact]
    public void Huge_images_are_rejected_from_the_header_and_garbage_is_unsupported()
    {
        var processor = new SkiaImageProcessor();

        Assert.Equal("image_too_large", Assert.Throws<MediaRejectedException>(() => processor.Process(PngHeader(8000, 6000))).Code);
        Assert.Equal("unsupported_media", Assert.Throws<MediaRejectedException>(() => processor.Process("<svg onload=alert(1)>"u8.ToArray())).Code);
    }

    [Theory]
    [InlineData("unsupported_media")]
    [InlineData("image_too_large")]
    [InlineData("photos_limit")]
    [InlineData("storage_limit")]
    public void Media_codes_have_a_spanish_detail(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(200, 80, 40));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    // Inserta un segmento APP1 Exif (TIFF mínimo + texto) justo después de SOI.
    private static byte[] WithExif(byte[] jpeg, string secret)
    {
        byte[] payload = [.. "Exif\0\0"u8.ToArray(), .. "MM\0*\0\0\0\b"u8.ToArray(), 0, 0, 0, 0, 0, 0, .. System.Text.Encoding.ASCII.GetBytes(secret)];
        var length = payload.Length + 2;
        return [jpeg[0], jpeg[1], 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, .. jpeg[2..]];
    }

    // PNG con encabezado de `width` × `height` y datos mínimos.
    private static byte[] PngHeader(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        byte[] header = [(byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width, (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height, 8, 2, 0, 0, 0];
        Chunk(stream, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(new byte[16]);
        }

        Chunk(stream, "IDAT", compressed.ToArray());
        Chunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var name = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write([(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length]);
        stream.Write(name);
        stream.Write(data);
        var crc = Crc32([.. name, .. data]);
        stream.Write([(byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc]);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
