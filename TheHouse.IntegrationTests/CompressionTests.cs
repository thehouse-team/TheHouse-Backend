using System.IO.Compression;
using System.Net;
using System.Text;

namespace TheHouse.IntegrationTests;

public sealed class CompressionTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("identity", null)]
    [InlineData("deflate", null)]
    [InlineData("gzip;q=0, br;q=0", null)]
    [InlineData("*;q=0", null)]
    [InlineData("gzip", "gzip")]
    [InlineData("br", "br")]
    [InlineData("gzip;q=1, br;q=0", "gzip")]
    [InlineData("gzip;q=0.5, br;q=1", "br")]
    [InlineData("br;q=0.5, gzip;q=1", "gzip")]
    public async Task Accept_encoding_is_negotiated_and_payload_roundtrips(string? accept, string? encoding)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        if (accept is not null) client.DefaultRequestHeaders.AcceptEncoding.ParseAdd(accept);
        using var response = await client.GetAsync("/api/v1/foundation-test/public/4096");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(encoding, response.Content.Headers.ContentEncoding.SingleOrDefault());
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (encoding is not null)
        {
            Assert.True(bytes.Length < 4096);
            await using Stream decoder = encoding == "br"
                ? new BrotliStream(new MemoryStream(bytes), CompressionMode.Decompress)
                : new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var reader = new StreamReader(decoder, Encoding.UTF8);
            Assert.Equal(new string('x', 4096), await reader.ReadToEndAsync());
        }
        else Assert.Equal(new string('x', 4096), Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData(1023, null)]
    [InlineData(1024, "gzip")]
    [InlineData(1048577, null)]
    public async Task Threshold_and_bounded_buffer_are_respected(int size, string? encoding)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        using var response = await client.GetAsync($"/api/v1/foundation-test/public/{size}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(encoding, response.Content.Headers.ContentEncoding.SingleOrDefault());
        if (encoding is null) Assert.Equal(size, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Theory]
    [InlineData("cookie")]
    [InlineData("private")]
    [InlineData("no-store")]
    [InlineData("no-transform")]
    [InlineData("mime")]
    public async Task Sensitive_headers_and_unapproved_mime_are_never_compressed(string variant)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
        using var response = await client.GetAsync($"/api/v1/foundation-test/public/4096?variant={variant}");
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(4096, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [Theory]
    [InlineData("Authorization", "Bearer non-token-fixture")]
    [InlineData("Cookie", "fixture=true")]
    public async Task Requests_with_sensitive_headers_do_not_compress(string header, string value)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        client.DefaultRequestHeaders.TryAddWithoutValidation(header, value);
        using var response = await client.GetAsync("/api/v1/foundation-test/public/4096");
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Responses_without_explicit_public_opt_in_do_not_compress()
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
        using var response = await client.GetAsync("/api/v1/foundation-test/sensitive");
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }
}
