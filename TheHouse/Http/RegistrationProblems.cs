using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class RegistrationProblems
{
    public static bool IsRegistration(HttpContext context) => HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(context.Request.Path.Value?.TrimEnd('/'), RegistrationEndpoint.Path, StringComparison.OrdinalIgnoreCase);

    // El contrato específico de NV-04 usa títulos en inglés y detail/instance seguros.
    // No se cambian las respuestas de Foundation para otros endpoints.
    public static void Normalize(ProblemDetails problem, int status)
    {
        problem.Title = status switch
        {
            400 => "Validation failed",
            409 => "Email already registered",
            413 => "Payload too large",
            415 => "Unsupported media type",
            429 => "Too many requests",
            503 => "Service unavailable",
            504 => "Gateway timeout",
            _ => "Internal server error"
        };
        problem.Detail = "The registration request could not be completed.";
        problem.Instance = RegistrationEndpoint.Path;
    }
}
