using Microsoft.OpenApi;
using Microsoft.AspNetCore.Mvc;

namespace TheHouse.Http;

public static class ApiConventions
{
    public const string Version = "v1";
    public const string Prefix = "/api/" + Version;

    public static void AddFoundationOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(Version, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "TheHouse Foundation";
                document.Info.Version = Version;
                document.Info.Description = "Foundation U2. API futura bajo /api/v1; health es operacional y no versionado. " +
                    "Sin endpoints de negocio ni autenticación JWT implementada. Errores RFC 9457 con traceId.";
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
                {
                    schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
                    schema.Properties["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 64 };
                    schema.Required ??= new HashSet<string>();
                    foreach (var property in new[] { "type", "title", "status", "traceId" })
                        schema.Required.Add(property);
                }
                return Task.CompletedTask;
            });
        });
    }
}
