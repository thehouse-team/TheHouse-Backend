using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;

namespace TheHouse.Http;

public sealed class PublicResponseCompressionMiddleware(RequestDelegate next,
    IResponseCompressionProvider provider, IOptions<FoundationOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<PublicResponseCompressionAttribute>() is null ||
            context.User.Identity?.IsAuthenticated == true ||
            context.Request.Headers.ContainsKey("Authorization") || context.Request.Headers.ContainsKey("Cookie") ||
            !HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        // También en respuestas identity: evita mezclar variantes en caches intermedias.
        if (!context.Response.Headers.GetCommaSeparatedValues("Vary").Contains("Accept-Encoding", StringComparer.OrdinalIgnoreCase))
            context.Response.Headers.AppendCommaSeparatedValues("Vary", "Accept-Encoding");

        var original = context.Response.Body;
        using var buffer = new BoundedResponseBuffer(original, 1048576);
        context.Response.Body = buffer;
        try { await next(context); }
        finally { context.Response.Body = original; }

        if (buffer.Passthrough) return;
        buffer.Buffered.Position = 0;
        var cacheControl = context.Response.Headers.CacheControl.ToString();
        if (!context.Response.HasStarted && context.Response.StatusCode == 200 &&
            buffer.Buffered.Length >= options.Value.CompressionMinimumBytes &&
            !context.Response.Headers.ContainsKey("Set-Cookie") &&
            !cacheControl.Contains("private", StringComparison.OrdinalIgnoreCase) &&
            !cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase) &&
            !cacheControl.Contains("no-transform", StringComparison.OrdinalIgnoreCase))
        {
            // El middleware oficial negocia Accept-Encoding (incluido q=0), MIME y HTTPS.
            var compression = new ResponseCompressionMiddleware(
                current => buffer.Buffered.CopyToAsync(current.Response.Body, current.RequestAborted), provider);
            await compression.Invoke(context);
        }
        else
        {
            await buffer.Buffered.CopyToAsync(original, context.RequestAborted);
        }
    }

    private sealed class BoundedResponseBuffer(Stream destination, int limit) : Stream
    {
        public MemoryStream Buffered { get; } = new();
        public bool Passthrough { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Buffered.Length;
        public override long Position { get => Buffered.Position; set => throw new NotSupportedException(); }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            if (!Passthrough && Buffered.Length + bytes.Length <= limit)
            {
                await Buffered.WriteAsync(bytes, cancellationToken);
                return;
            }
            await SpillAsync(cancellationToken);
            await destination.WriteAsync(bytes, cancellationToken);
        }
        public override Task WriteAsync(byte[] bytes, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(bytes.AsMemory(offset, count), cancellationToken).AsTask();
        public override void Write(byte[] bytes, int offset, int count)
        {
            if (!Passthrough && Buffered.Length + count <= limit)
                Buffered.Write(bytes, offset, count);
            else
            {
                Spill();
                destination.Write(bytes, offset, count);
            }
        }
        private async Task SpillAsync(CancellationToken cancellationToken)
        {
            if (Passthrough) return;
            Passthrough = true;
            Buffered.Position = 0;
            await Buffered.CopyToAsync(destination, cancellationToken);
            Buffered.SetLength(0);
        }
        private void Spill()
        {
            if (Passthrough) return;
            Passthrough = true;
            Buffered.Position = 0;
            Buffered.CopyTo(destination);
            Buffered.SetLength(0);
        }
        // El opt-in es para respuestas finitas, no streaming/SSE. Flush no amplía el buffer.
        public override void Flush() { if (Passthrough) destination.Flush(); }
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Passthrough ? destination.FlushAsync(cancellationToken) : Task.CompletedTask;
        protected override void Dispose(bool disposing)
        {
            if (disposing) Buffered.Dispose();
            base.Dispose(disposing);
        }
        public override int Read(byte[] bytes, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
