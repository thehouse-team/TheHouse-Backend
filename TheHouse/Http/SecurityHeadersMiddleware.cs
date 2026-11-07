namespace TheHouse.Http;

// NV-01-R14 (1.2.0-u2): encabezados de seguridad comunes a todas las
// respuestas (200, 304, 4xx, 5xx y health). Se aplican en OnStarting para
// que sobrevivan a cualquier Response.Clear() de los manejadores de error.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentTypeOptions = "X-Content-Type-Options";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(
            static state =>
            {
                var response = (HttpResponse)state;
                response.Headers[ContentTypeOptions] = "nosniff";
                return Task.CompletedTask;
            },
            context.Response);

        return next(context);
    }
}
