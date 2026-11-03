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
        problem.Title = status switch
        {
            400 => "Validation failed",
            401 => "Invalid credentials",
            413 => "Payload too large",
            415 => "Unsupported media type",
            429 => "Too many requests",
            503 => "Service unavailable",
            504 => "Gateway timeout",
            _ => "Internal server error"
        };

        problem.Detail =
            "The login request could not be completed.";

        problem.Instance = LoginEndpoint.Path;
    }
}
