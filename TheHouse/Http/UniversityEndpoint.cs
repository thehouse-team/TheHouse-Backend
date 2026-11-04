using System.Globalization;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Universities;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Http;

public static class UniversityEndpoint
{
    public const string ListPath =
        ApiConventions.Prefix + "/universities";

    public const string DetailPath =
        ApiConventions.Prefix + "/universities/{id}";

    public static void MapUniversities(
        this WebApplication app)
    {
        app.MapGet(
                ListPath,
                ListAsync)
            .WithName("ListUniversities")
            .WithTags("Universities")
            .WithSummary(
                "List public universities")
            .Produces<UniversityListResponse>(
                StatusCodes.Status200OK)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .DisableRateLimiting();

        app.MapGet(
                DetailPath,
                DetailAsync)
            .WithName("GetUniversity")
            .WithTags("Universities")
            .WithSummary(
                "Get public university details")
            .Produces<UniversityResponse>(
                StatusCodes.Status200OK)
            .ProducesValidationProblem(
                StatusCodes.Status400BadRequest)
            .ProducesProblem(
                StatusCodes.Status404NotFound)
            .ProducesProblem(
                StatusCodes.Status429TooManyRequests)
            .ProducesProblem(
                StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(
                StatusCodes.Status500InternalServerError)
            .AllowAnonymous()
            .DisableRateLimiting();
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        UniversityQueryService universities,
        IRateLimitStore rateLimitStore,
        UniversityRateLimitOptions rateLimits)
    {
        try
        {
            await EnforceRateLimit(
                context,
                rateLimitStore,
                rateLimits);

            var page =
                ParseQueryInteger(
                    context,
                    "page",
                    UniversityQueryValidation.DefaultPage);

            var pageSize =
                ParseQueryInteger(
                    context,
                    "pageSize",
                    UniversityQueryValidation.DefaultPageSize);

            var result =
                await universities.ListAsync(
                    page,
                    pageSize,
                    context.RequestAborted);

            return Results.Ok(result);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static async Task<IResult> DetailAsync(
        string id,
        HttpContext context,
        UniversityQueryService universities,
        IRateLimitStore rateLimitStore,
        UniversityRateLimitOptions rateLimits)
    {
        try
        {
            await EnforceRateLimit(
                context,
                rateLimitStore,
                rateLimits);

            var result =
                await universities.GetByIdAsync(
                    id,
                    context.RequestAborted);

            return Results.Ok(result);
        }
        catch (RequestValidationException)
        {
            return Error(context, 400);
        }
        catch (NotFoundException)
        {
            return Error(context, 404);
        }
        catch (RateLimitExceededException exception)
        {
            context.Response.Headers.RetryAfter =
                exception.RetryAfterSeconds.ToString(
                    CultureInfo.InvariantCulture);

            return Error(context, 429);
        }
        catch (DependencyUnavailableException)
        {
            return Error(context, 503);
        }
    }

    private static int ParseQueryInteger(
        HttpContext context,
        string name,
        int defaultValue)
    {
        if (!context.Request.Query.TryGetValue(
                name,
                out var values))
        {
            return defaultValue;
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]) ||
            !int.TryParse(
                values[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new RequestValidationException(
                name,
                $"{name} must be an integer.");
        }

        return value;
    }

    private static async Task EnforceRateLimit(
        HttpContext context,
        IRateLimitStore store,
        UniversityRateLimitOptions options)
    {
        var address =
            NormalizeAddress(
                context.Connection.RemoteIpAddress);

        var decision =
            await store.ConsumeAsync(
                RateLimitKeys.UniversityIp(address),
                options.RequestsPerWindow,
                options.Window,
                context.RequestAborted);

        if (!decision.IsAllowed)
        {
            throw new RateLimitExceededException(
                options.RetryAfterSeconds);
        }
    }

    private static string NormalizeAddress(
        System.Net.IPAddress? address)
    {
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.ToString() ?? "unknown";
    }

    private static IResult Error(
        HttpContext context,
        int status) =>
        Results.Problem(
            ProblemResponses.Create(
                context,
                status,
                validation: status == 400));
}
