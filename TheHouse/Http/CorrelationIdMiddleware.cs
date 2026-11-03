using System.Diagnostics;

namespace TheHouse.Http;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var values = context.Request.Headers[Header];
        var supplied = values.Count == 1 ? values[0] : null;
        var id = IsValid(supplied) ? supplied! : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("thehouse.correlation_id", id);
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next(context);
    }

    public static bool IsValid(string? value) => value is { Length: >= 1 and <= 64 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
