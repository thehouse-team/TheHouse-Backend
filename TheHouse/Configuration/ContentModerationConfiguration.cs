using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Moderation;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.Configuration;

// NV-21 1.2.0: revisión automática antes de guardar. Con
// `ContentSafety__Endpoint` (un recurso de Azure AI Content Safety) se usa la
// identidad administrada de la Container App; sin él, solo la lista de
// términos (por ejemplo, en desarrollo o antes del apply del recurso).
public static class ContentModerationConfiguration
{
    public static void AddContentModeration(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("ContentSafety");
        var options = new ContentModerationOptions
        {
            Threshold = section.GetValue("Threshold", 4),
            Thresholds = section.GetSection("Thresholds").Get<Dictionary<string, int>>() ?? [],
            Blocklist = section.GetSection("Blocklist").Get<string[]>() ?? [],
        };
        var endpoint = section["Endpoint"];
        var clientId = section["IdentityClientId"];
        if (options.Threshold is not (2 or 4 or 6) ||
            options.Thresholds.Any(pair => pair.Key is not ("Hate" or "Sexual" or "Violence" or "SelfHarm") || pair.Value is not (2 or 4 or 6)) ||
            (!string.IsNullOrEmpty(endpoint) && !IsContentSafetyEndpoint(endpoint)) ||
            (!string.IsNullOrEmpty(clientId) && !Guid.TryParse(clientId, out _)))
        {
            throw new OptionsValidationException(
                Options.DefaultName,
                typeof(ContentModerationConfiguration),
                ["Configuración inválida en ContentSafety; no se muestran valores."]);
        }

        builder.Services.AddSingleton(options);
        if (string.IsNullOrEmpty(endpoint))
        {
            builder.Services.AddSingleton<IContentSafetyClient, DisabledContentSafetyClient>();
        }
        else
        {
            var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            builder.Services.AddSingleton<IManagedIdentityTokenSource>(provider => new ContainerAppsIdentityTokenSource(
                http,
                Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"),
                Environment.GetEnvironmentVariable("IDENTITY_HEADER"),
                section["IdentityClientId"],
                provider.GetRequiredService<TimeProvider>()));
            builder.Services.AddSingleton<IContentSafetyClient>(provider =>
            {
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("TheHouse.ContentSafety");
                return new AzureContentSafetyClient(
                    http,
                    new Uri(endpoint.TrimEnd('/') + "/"),
                    provider.GetRequiredService<IManagedIdentityTokenSource>(),
                    reason => logger.LogWarning("Content Safety no disponible ({Reason}); la escritura respondió 503.", reason));
            });
        }

        builder.Services.AddSingleton<IModerationRejectionRepository>(provider =>
        {
            var database = provider.GetService<IMongoDatabase>();
            return database is null
                ? new UnavailableModerationRejectionRepository()
                : new MongoModerationRejectionRepository(database);
        });
        builder.Services.AddSingleton<IContentModerator, ContentModerationService>();
        builder.Services.AddSingleton<ModerationRejectionService>();
    }

    // Solo un recurso de Azure AI Services por https (sin SSRF por configuración).
    public static bool IsContentSafetyEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase);
}
