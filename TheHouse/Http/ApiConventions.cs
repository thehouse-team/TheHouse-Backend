using Microsoft.OpenApi;
using Microsoft.AspNetCore.Mvc;
using TheHouse.Application.Contracts.Auth;

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
                document.Info.Title = "TheHouse U2 — Foundation, Registration, Login, Logout, Properties y Universities";
                document.Info.Version = Version;
                document.Info.Description = "Foundation, Registration, Login, Logout, Properties y Universities U2 bajo /api/v1; health es operacional y no versionado. " +
                    "JWT de acceso revocable vía Logout. Errores RFC 9457 con traceId.";
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
                if (context.JsonTypeInfo.Type == typeof(RegisterRequest) || context.JsonTypeInfo.Type == typeof(LoginRequest))
                {
                    schema.AdditionalPropertiesAllowed = false;
                    if (schema.Properties?.TryGetValue("password", out var password) == true && password is OpenApiSchema value)
                    {
                        value.Format = "password";
                        value.WriteOnly = true;
                        value.MinLength = context.JsonTypeInfo.Type == typeof(RegisterRequest) ? 12 : 1;
                        value.MaxLength = 128;
                    }
                }
                return Task.CompletedTask;
            });
        });
    }
}
