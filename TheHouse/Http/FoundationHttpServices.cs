using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TheHouse.Configuration;

namespace TheHouse.Http;

public static class FoundationHttpServices
{
    public static void AddFoundationHttp(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            ProblemResponses.Normalize(context.HttpContext, context.ProblemDetails,
                context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode));

        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.MaxDepth = 32;
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        }).ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        {
            // No devolver mensajes del binder: pueden incluir el valor enviado.
            var result = new BadRequestObjectResult(ProblemResponses.Create(context.HttpContext, 400, validation: true));
            result.ContentTypes.Add("application/problem+json");
            return result;
        });

        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddRequestTimeouts();
        services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<FoundationOptions>>((options, limits) =>
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(limits.Value.RequestTimeoutSeconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
                WriteTimeoutResponse = context => ProblemResponses.WriteAsync(context, 504)
            });
    }
}
