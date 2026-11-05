using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class RegistrationProblems
{
    public static bool IsRegistration(HttpContext context) => HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(context.Request.Path.Value?.TrimEnd('/'), RegistrationEndpoint.Path, StringComparison.OrdinalIgnoreCase);

    // El contrato específico de NV-04 usa títulos y mensajes en español,
    // con detail/instance seguros. No se cambian las respuestas de otros endpoints.
    public static void Normalize(ProblemDetails problem, int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
        problem.Detail = status switch
        {
            400 => "La solicitud de registro no es válida.",
            409 => "El correo electrónico ya está registrado.",
            413 => "La solicitud de registro supera el tamaño permitido.",
            415 => "El tipo de contenido no está admitido.",
            429 => "Se superó el límite de solicitudes de registro.",
            503 => "El registro no está disponible temporalmente.",
            504 => "El registro no respondió a tiempo.",
            _ => "No fue posible completar el registro."
        };
        problem.Instance = RegistrationEndpoint.Path;
    }
}
