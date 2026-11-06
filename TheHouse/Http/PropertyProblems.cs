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
        problem.Title = ProblemResponses.TitleFor(status);
    }
}
