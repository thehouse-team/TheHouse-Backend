using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;

namespace TheHouse.Http;

public sealed class RequestBodyLimitMiddleware(RequestDelegate next, IOptions<FoundationOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var limit = options.Value.MaxRequestBodyBytes;
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = limit;
        if (context.Request.ContentLength > limit)
            throw TooLarge();

        var original = context.Request.Body;
        using var limited = new LimitedReadStream(original, limit);
        context.Request.Body = limited;
        try { await next(context); }
        finally { context.Request.Body = original; }
    }

    private static BadHttpRequestException TooLarge() => new("Solicitud demasiado grande.", 413);

    private sealed class LimitedReadStream(Stream inner, long limit) : Stream
    {
        private long consumed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => consumed; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        private int Count(int count)
        {
            consumed += count;
            if (consumed > limit) throw TooLarge();
            return count;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
