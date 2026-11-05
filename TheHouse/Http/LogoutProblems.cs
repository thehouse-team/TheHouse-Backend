using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

// NV-06: títulos de logout en español: No autorizado /
// Demasiadas solicitudes / Servicio no disponible / Error interno
// del servidor, sin detalles internos.
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
        problem.Title = ProblemResponses.TitleFor(status);

        // Sin `detail` ni `instance`: el contrato compartido exige
        // respuestas sanitizadas y sin rutas internas.
        problem.Detail = null;
        problem.Instance = null;
    }
}
