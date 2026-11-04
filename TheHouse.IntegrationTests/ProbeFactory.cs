using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace TheHouse.IntegrationTests;

// Los endpoints sintéticos existen únicamente en el assembly de pruebas.
public sealed class ProbeFactory(IReadOnlyDictionary<string, string?>? settings = null) : FoundationFactory(settings)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(FoundationTestController).Assembly));
    }
}
