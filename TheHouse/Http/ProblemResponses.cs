using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class ProblemResponses
{
    public static ProblemDetails Create(HttpContext context, int status, bool validation = false)
    {
        ProblemDetails problem = validation
            ? new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = ["Entrada invÃ¡lida."] })
            : new ProblemDetails();
        Normalize(context, problem, status);
        return problem;
    }

    public static void Normalize(HttpContext context, ProblemDetails problem, int status)
    {
        problem.Status = status;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Title = status switch
        {
            400 => "Solicitud invÃ¡lida",
            401 => "AutenticaciÃ³n requerida",
            403 => "Acceso denegado",
            404 => "Recurso no encontrado",
            405 => "MÃ©todo no permitido",
            408 => "Tiempo de solicitud agotado",
            409 => "Conflicto",
            413 => "Solicitud demasiado grande",
            415 => "Tipo de contenido no admitido",
            429 => "Demasiadas solicitudes",
            503 => "Servicio no disponible",
            504 => "Tiempo de respuesta agotado",
            _ => status >= 500 ? "Error del servidor" : "Solicitud rechazada"
        };
        problem.Detail = null;
        problem.Instance = null;
        problem.Extensions.Clear();
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (RegistrationProblems.IsRegistration(context))
            RegistrationProblems.Normalize(problem, status);
        else if (LoginProblems.IsLogin(context))
            LoginProblems.Normalize(problem, status);
        else if (LogoutProblems.IsLogout(context))
            LogoutProblems.Normalize(problem, status);
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
