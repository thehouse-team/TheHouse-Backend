using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

// NV-06: títulos y detalle de logout definidos por la spec 9:
// Unauthorized / Too many requests / Service unavailable /
// Internal server error, sin detalles internos.
public static class LogoutProblems
{
    public static bool IsLogout(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(
            context.Request.Path.Value?.TrimEnd('/'),
            LogoutEndpoint.Path,
            StringComparison.OrdinalIgnoreCase);

    public static void Normalize(
        ProblemDetails problem,
        int status)
    {
        problem.Title = status switch
        {
            400 => "Validation failed",
            401 => "Unauthorized",
            429 => "Too many requests",
            503 => "Service unavailable",
            500 => "Internal server error",
            _ => "Internal server error"
        };

        // Sin `detail` ni `instance`: el contrato compartido exige
        // respuestas sanitizadas y sin rutas internas.
        problem.Detail = null;
        problem.Instance = null;
    }
}
