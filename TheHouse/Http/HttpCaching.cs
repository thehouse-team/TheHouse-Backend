using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using TheHouse.Configuration;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace TheHouse.Http;

// Caché HTTP condicional para lecturas públicas (NV-07-R08, NV-08-R08).
// Solo se usa en respuestas 200 de endpoints anónimos; los errores conservan
// no-store y no llevan ETag.
public static class HttpCaching
{
    public const string JsonContentType = "application/json; charset=utf-8";

    public static IResult PublicJson<T>(HttpContext context, T value)
    {
        var services = context.RequestServices;
        var serializer = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var maxAge = services.GetRequiredService<IOptions<FoundationOptions>>().Value.PublicCacheMaxAgeSeconds;

        // El ETag se calcula sobre el cuerpo sin comprimir; al ser débil sigue
        // siendo válido para cualquier codificación de contenido.
        var body = JsonSerializer.SerializeToUtf8Bytes(value, serializer);
        var etag = new EntityTagHeaderValue($"\"{Base64Url(SHA256.HashData(body))}\"", isWeak: true);

        var headers = context.Response.GetTypedHeaders();
        headers.ETag = etag;
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(maxAge)
        };

        if (!context.Response.Headers.Vary.Any(value => value?.Contains("Accept-Encoding", StringComparison.OrdinalIgnoreCase) == true))
            context.Response.Headers.Append(HeaderNames.Vary, "Accept-Encoding");

        return Matches(context.Request, etag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Bytes(body, JsonContentType);
    }

    // Comparación débil de If-None-Match (RFC 9110 §13.1.2), incluidos listas y "*".
    internal static bool Matches(HttpRequest request, EntityTagHeaderValue current) =>
        request.GetTypedHeaders().IfNoneMatch.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) ||
            candidate.Compare(current, useStrongComparison: false));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
