using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

// NV-15/NV-17: mensajes en español para favoritos y visitas. El 404 no
// distingue entre recurso inexistente y ajeno (BOLA).
public static class UserResourceProblems
{
    public const string CodeItem = "thehouse.problem.code";

    // Agrega `code` y su `detail` si el endpoint lo indicó para este status.
    public static void ApplyCode(HttpContext context, ProblemDetails problem, int status)
    {
        if (status is 409 or 422 &&
            context.Items.TryGetValue(CodeItem, out var value) &&
            value is string code &&
            DetailFor(code) is { } detail)
        {
            problem.Extensions["code"] = code;
            problem.Detail = detail;
        }
    }

    public static bool IsFavorites(HttpContext context) =>
        context.Request.Path.StartsWithSegments(FavoritesEndpoint.BasePath, StringComparison.OrdinalIgnoreCase);

    public static bool IsVisits(HttpContext context) =>
        context.Request.Path.StartsWithSegments(VisitsEndpoint.BasePath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(VisitsEndpoint.MinePath, StringComparison.OrdinalIgnoreCase);

    public static void NormalizeFavorites(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "El identificador de la propiedad o la paginación no son válidos.",
            401 => "Se requiere una sesión válida.",
            404 => "La propiedad no existe.",
            409 => "Se alcanzó el límite de favoritos.",
            429 => "Se superó el límite de solicitudes de favoritos.",
            503 => "Los favoritos no están disponibles temporalmente.",
            504 => "Los favoritos no respondieron a tiempo.",
            _ => "No fue posible procesar los favoritos.",
        };
    }

    public static void NormalizeVisits(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "La solicitud de visita no es válida.",
            401 => "Se requiere una sesión válida.",
            404 => "La visita o la propiedad no existe.",
            409 => "La solicitud entra en conflicto con otra visita.",
            413 => "La solicitud supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            422 => "El horario no cumple las reglas de visita.",
            429 => "Se superó el límite de solicitudes de visitas.",
            503 => "Las visitas no están disponibles temporalmente.",
            504 => "Las visitas no respondieron a tiempo.",
            _ => "No fue posible procesar la visita.",
        };
    }

    public static string? DetailFor(string code) => code switch
    {
        "favorites_limit" => "Se alcanzó el máximo de 200 favoritos.",
        "visit_in_progress" => "Ya hay una solicitud de visita en proceso; intenta de nuevo.",
        "idempotency_key_reused" => "La Idempotency-Key ya se usó con otra solicitud.",
        "active_visit_for_property" => "Ya tienes una visita solicitada para esta propiedad.",
        "schedule_overlap" => "Ya tienes otra visita a menos de 60 minutos de ese horario.",
        "active_visits_limit" => "Alcanzaste el máximo de 5 visitas solicitadas.",
        "visit_state" => "La visita no se puede cancelar en su estado actual.",
        "visit_in_past" => "No se puede cancelar una visita cuyo horario ya pasó.",
        "too_soon" => "La visita debe solicitarse con al menos 2 horas de anticipación.",
        "too_far" => "La visita no puede programarse a más de 60 días.",
        "not_quarter_hour" => "La hora debe ser múltiplo de 15 minutos.",
        "outside_hours" => "La visita debe estar entre 08:00 y 20:00 (hora de Ciudad de México).",
        _ => null,
    };
}
