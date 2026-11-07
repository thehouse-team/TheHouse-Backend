using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.TestDoubles;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

// "Testing" evita cargar appsettings.Development.json (configuración local con
// conexiones reales); las pruebas nunca deben depender de ese archivo.
public class FoundationFactory(IReadOnlyDictionary<string, string?>? settings = null, string environment = ApiConventions.TestingEnvironment)
    : WebApplicationFactory<Program>
{
    // NV-21: moderación en memoria para todas las fábricas.
    public InMemoryModerationRepositories Moderation { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStaffRoleRepository>();
            services.RemoveAll<IVerificationReviewRepository>();
            services.RemoveAll<IReportRepository>();
            services.RemoveAll<IUserSuspensionRepository>();
            services.RemoveAll<IAuditRepository>();
            services.AddSingleton<IStaffRoleRepository>(Moderation);
            services.AddSingleton<IVerificationReviewRepository>(Moderation);
            services.AddSingleton<IReportRepository>(Moderation);
            services.AddSingleton<IUserSuspensionRepository>(Moderation);
            services.AddSingleton<IAuditRepository>(Moderation);
        });

        builder.UseEnvironment(environment);
        // UseSetting se aplica antes del composition root del minimal host.
        if (settings is not null)
            foreach (var setting in settings)
                builder.UseSetting(setting.Key, setting.Value);
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}
