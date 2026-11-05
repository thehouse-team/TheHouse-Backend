using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class UniversityProblems
{
    public static bool IsUniversity(
        HttpContext context) =>
        context.Request.Path.StartsWithSegments(
            ApiConventions.Prefix + "/universities",
            StringComparison.OrdinalIgnoreCase);

    public static void Normalize(
        ProblemDetails problem,
        int status)
    {
        problem.Title = ProblemResponses.TitleFor(status);
    }
}
