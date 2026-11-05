using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class LoginProblems
{
    public static bool IsLogin(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(
            context.Request.Path.Value?.TrimEnd('/'),
            LoginEndpoint.Path,
            StringComparison.OrdinalIgnoreCase);

    public static void Normalize(
        ProblemDetails problem,
        int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "La solicitud de inicio de sesión no es válida.",
            401 => "Las credenciales proporcionadas no son válidas.",
            413 => "La solicitud de inicio de sesión supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes de inicio de sesión.",
            503 => "El inicio de sesión no está disponible temporalmente.",
            504 => "El inicio de sesión no respondió a tiempo.",
            _ => "No fue posible completar el inicio de sesión."
        };

        problem.Instance = LoginEndpoint.Path;
    }
}
