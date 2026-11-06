using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

// NV-10: mensajes en español para POST /auth/refresh y GET /users/me.
// El 401 del refresh es uniforme: no distingue token desconocido,
// vencido, revocado o reusado.
public static class SessionProblems
{
    public static bool IsRefresh(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(
            context.Request.Path.Value?.TrimEnd('/'),
            RefreshEndpoint.Path,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsMe(HttpContext context) =>
        string.Equals(
            context.Request.Path.Value?.TrimEnd('/'),
            UsersMeEndpoint.Path,
            StringComparison.OrdinalIgnoreCase);

    public static void NormalizeRefresh(
        ProblemDetails problem,
        int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "La solicitud de renovación de sesión no es válida.",
            401 => "La sesión no es válida o expiró. Inicia sesión de nuevo.",
            413 => "La solicitud de renovación supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes de renovación.",
            503 => "La renovación de sesión no está disponible temporalmente.",
            504 => "La renovación de sesión no respondió a tiempo.",
            _ => "No fue posible renovar la sesión."
        };
        problem.Instance = RefreshEndpoint.Path;
    }

    public static void NormalizeMe(
        ProblemDetails problem,
        int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            401 => "Se requiere una sesión válida.",
            503 => "La consulta de la cuenta no está disponible temporalmente.",
            504 => "La consulta de la cuenta no respondió a tiempo.",
            _ => "No fue posible consultar la cuenta."
        };
        problem.Instance = UsersMeEndpoint.Path;
    }
}
