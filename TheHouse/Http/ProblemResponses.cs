using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class ProblemResponses
{
    public static ProblemDetails Create(HttpContext context, int status, bool validation = false)
    {
        ProblemDetails problem = validation
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = ["Entrada inválida."] })
            : new ProblemDetails();
        Normalize(context, problem, status);
        return problem;
    }

    public static string TitleFor(int status) => status switch
    {
        400 => "Validación fallida",
        401 => "No autorizado",
        403 => "Acceso denegado",
        404 => "Recurso no encontrado",
        405 => "Método no permitido",
        408 => "Tiempo de solicitud agotado",
        409 => "Conflicto",
        413 => "Solicitud demasiado grande",
        415 => "Tipo de contenido no admitido",
        422 => "Solicitud no procesable",
        429 => "Demasiadas solicitudes",
        500 => "Error interno del servidor",
        503 => "Servicio no disponible",
        504 => "Tiempo de respuesta agotado",
        _ => status >= 500 ? "Error del servidor" : "Solicitud rechazada"
    };

    public static void Normalize(HttpContext context, ProblemDetails problem, int status)
    {
        problem.Status = status;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Title = TitleFor(status);
        problem.Detail = null;
        problem.Instance = null;
        problem.Extensions.Clear();
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (MessagingEndpoint.IsMessaging(context))
            UserResourceProblems.NormalizeMessaging(problem, status);
        else if (VisitsEndpoint.IsLandlordVisits(context))
            UserResourceProblems.NormalizeVisits(problem, status);
        else if (UserResourceProblems.IsAdmin(context))
            UserResourceProblems.NormalizeAdmin(problem, status);
        else if (RegistrationProblems.IsRegistration(context))
            RegistrationProblems.Normalize(problem, status);
        else if (LoginProblems.IsLogin(context))
            LoginProblems.Normalize(problem, status);
        else if (LogoutProblems.IsLogout(context))
            LogoutProblems.Normalize(problem, status);
        else if (SessionProblems.IsRefresh(context))
            SessionProblems.NormalizeRefresh(problem, status);
        else if (SessionProblems.IsMe(context))
            SessionProblems.NormalizeMe(problem, status);
        else if (UserResourceProblems.IsLandlord(context))
            UserResourceProblems.NormalizeLandlord(problem, status);
        else if (UserResourceProblems.IsProfile(context))
            UserResourceProblems.NormalizeProfile(problem, status);
        else if (UserResourceProblems.IsFavorites(context))
            UserResourceProblems.NormalizeFavorites(problem, status);
        else if (UserResourceProblems.IsVisits(context))
            UserResourceProblems.NormalizeVisits(problem, status);
        else if (PropertyProblems.IsProperty(context))
            PropertyProblems.Normalize(problem, status);
        else if (UniversityProblems.IsUniversity(context))
            UniversityProblems.Normalize(problem, status);
        else if (SearchEndpoint.IsSearch(context))
            SearchEndpoint.Normalize(problem, status);

        // Código estable (M-BE-061) en cualquier ruta que lo haya indicado.
        UserResourceProblems.ApplyCode(context, problem, status);
    }

    public static async Task WriteAsync(HttpContext context, int status, bool validation = false)
    {
        if (context.Response.HasStarted || context.RequestAborted.IsCancellationRequested)
            return;
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        var problem = Create(context, status, validation);
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
