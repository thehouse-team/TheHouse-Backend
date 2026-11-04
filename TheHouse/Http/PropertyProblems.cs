using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class PropertyProblems
{
    public static bool IsProperty(
        HttpContext context) =>
        context.Request.Path.StartsWithSegments(
            ApiConventions.Prefix + "/properties",
            StringComparison.OrdinalIgnoreCase);

    public static void Normalize(
        ProblemDetails problem,
        int status)
    {
        problem.Title = status switch
        {
            StatusCodes.Status400BadRequest =>
                "Validation failed",

            StatusCodes.Status404NotFound =>
                "Not found",

            StatusCodes.Status429TooManyRequests =>
                "Too many requests",

            StatusCodes.Status503ServiceUnavailable =>
                "Service unavailable",

            StatusCodes.Status500InternalServerError =>
                "Internal server error",

            _ => problem.Title
        };
    }
}
