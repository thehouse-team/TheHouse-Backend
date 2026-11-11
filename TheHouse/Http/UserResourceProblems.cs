using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

// NV-15/NV-17: mensajes en español para favoritos y visitas. El 404 no
// distingue entre recurso inexistente y ajeno (BOLA).
public static class UserResourceProblems
{
    public const string CodeItem = "thehouse.problem.code";

    public const string DetailItem = "thehouse.problem.detail";

    // Agrega `code` y su `detail` si el endpoint lo indicó para este status.
    public static void ApplyCode(HttpContext context, ProblemDetails problem, int status)
    {
        if (status is 403 or 409 or 422 &&
            context.Items.TryGetValue(CodeItem, out var value) &&
            value is string code &&
            DetailFor(code) is { } detail)
        {
            problem.Extensions["code"] = code;
            problem.Detail = context.Items.TryGetValue(DetailItem, out var specific) && specific is string text
                ? text
                : detail;
        }
    }

    public static bool IsFavorites(HttpContext context) =>
        context.Request.Path.StartsWithSegments(FavoritesEndpoint.BasePath, StringComparison.OrdinalIgnoreCase);

    public static bool IsVisits(HttpContext context) =>
        context.Request.Path.StartsWithSegments(VisitsEndpoint.BasePath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(VisitsEndpoint.MinePath, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdmin(HttpContext context) =>
        context.Request.Path.StartsWithSegments(AdminEndpoint.BasePath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(AdminEndpoint.ReportsPath, StringComparison.OrdinalIgnoreCase);

    public static void NormalizeAdmin(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "Los datos de la solicitud no son válidos.",
            401 => "Se requiere una sesión válida.",
            403 => "Esta acción requiere un rol administrativo.",
            404 => "El recurso no existe.",
            409 => "La solicitud entra en conflicto con el estado actual.",
            413 => "La solicitud supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes.",
            503 => "El servicio no está disponible temporalmente.",
            504 => "El servicio no respondió a tiempo.",
            _ => "No fue posible procesar la solicitud.",
        };
    }

    public static bool IsLandlord(HttpContext context) =>
        context.Request.Path.StartsWithSegments(LandlordEndpoint.BasePath, StringComparison.OrdinalIgnoreCase);

    public static void NormalizeLandlord(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "Los datos del arrendador, del edificio o del cuarto no son válidos.",
            401 => "Se requiere una sesión válida.",
            403 => "Esta acción requiere activar el modo arrendador.",
            404 => "El recurso no existe.",
            409 => "La solicitud entra en conflicto con el estado actual.",
            413 => "La solicitud supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes del modo arrendador.",
            503 => "El modo arrendador no está disponible temporalmente.",
            504 => "El modo arrendador no respondió a tiempo.",
            _ => "No fue posible procesar la solicitud.",
        };
    }

    public static bool IsProfile(HttpContext context) =>
        context.Request.Path.StartsWithSegments(ProfileEndpoint.ProfilePath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(ProfileEndpoint.UniversityPath, StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments(ProfileEndpoint.PreferencesPath, StringComparison.OrdinalIgnoreCase);

    public static void NormalizeProfile(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "Los datos del perfil o de las preferencias no son válidos.",
            401 => "Se requiere una sesión válida.",
            404 => "La universidad no existe.",
            413 => "La solicitud supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes del perfil.",
            503 => "El perfil no está disponible temporalmente.",
            504 => "El perfil no respondió a tiempo.",
            _ => "No fue posible procesar el perfil.",
        };
    }

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
            403 => "Esta acción requiere activar el modo arrendador.",
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

    // NV-16: solicitudes y chat.
    public static void NormalizeMessaging(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "Los datos de la solicitud o del mensaje no son válidos.",
            401 => "Se requiere una sesión válida.",
            403 => "Esta acción requiere activar el modo arrendador.",
            404 => "La solicitud, el cuarto o la conversación no existe.",
            409 => "La solicitud entra en conflicto con el estado actual.",
            413 => "La solicitud supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            422 => "La solicitud no cumple las reglas de contacto.",
            429 => "Se superó el límite de solicitudes o mensajes.",
            503 => "Las solicitudes y el chat no están disponibles temporalmente.",
            504 => "Las solicitudes y el chat no respondieron a tiempo.",
            _ => "No fue posible procesar la solicitud.",
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
        "visit_state" => "La visita no se puede cambiar en su estado actual.",
        "visit_in_past" => "La visita ya pasó y no se puede cambiar.",
        "student_schedule_overlap" => "El nuevo horario choca con otra visita del estudiante.",
        "too_soon" => "La visita debe solicitarse con al menos 2 horas de anticipación.",
        "too_far" => "La visita no puede programarse a más de 60 días.",
        "not_quarter_hour" => "La hora debe ser múltiplo de 15 minutos.",
        "outside_hours" => "La visita debe estar entre 08:00 y 20:00 (hora de Ciudad de México).",
        "landlord_exists" => "La cuenta ya tiene el modo arrendador activo.",
        "account_suspended" => "La cuenta está suspendida.",
        "role_required" => "Esta acción requiere un rol administrativo.",
        "own_building" => "No puedes revisar un edificio propio.",
        "report_exists" => "Ya tienes un reporte abierto sobre ese objetivo.",
        "report_state" => "El reporte ya fue resuelto.",
        "user_state" => "La cuenta no se puede cambiar en su estado actual.",
        "role_not_granted" => "La cuenta no tenía ese rol.",
        "landlord_required" => "Esta acción requiere activar el modo arrendador.",
        "buildings_limit" => "Se alcanzó el máximo de 20 edificios.",
        "rooms_limit" => "Se alcanzó el máximo de cuartos (200 por edificio y 30 por piso).",
        "room_number_taken" => "Ya existe un cuarto con ese número en el edificio.",
        "building_in_review" => "El edificio está en revisión y no admite cambios.",
        "building_state" => "El edificio no se puede enviar a revisión en su estado actual.",
        "building_has_rented_rooms" => "No se puede borrar un edificio con cuartos rentados.",
        "room_rented" => "No se puede borrar un cuarto rentado.",
        "room_state" => "La acción no es válida en el estado actual del cuarto.",
        "version_conflict" => "Los datos cambiaron desde la última lectura; vuelve a cargarlos.",
        "publication_incomplete" => "Faltan pasos para enviar el edificio a revisión.",
        "own_room" => "No puedes enviar una solicitud a un cuarto propio.",
        "active_request_exists" => "Ya tienes una solicitud activa para este cuarto.",
        "active_requests_limit" => "Alcanzaste el máximo de 10 solicitudes activas.",
        "request_in_progress" => "Ya hay una solicitud en proceso; intenta de nuevo.",
        "request_state" => "La solicitud no se puede cambiar en su estado actual.",
        "room_unavailable" => "El cuarto ya no está publicado.",
        "conversation_closed" => "La conversación está cerrada.",
        "participant_suspended" => "Una de las cuentas de la conversación está suspendida.",
        _ => null,
    };
}
