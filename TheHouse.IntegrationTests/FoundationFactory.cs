using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TheHouse.IntegrationTests;

public class FoundationFactory(IReadOnlyDictionary<string, string?>? settings = null, string environment = "Development")
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
