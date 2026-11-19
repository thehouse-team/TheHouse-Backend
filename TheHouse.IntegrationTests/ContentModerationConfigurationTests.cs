using Microsoft.Extensions.DependencyInjection;
using TheHouse.Application.UseCases;

namespace TheHouse.IntegrationTests;

// NV-21 1.2.0: los umbrales de appsettings.json se leen como se aprobaron
// (violencia y odio en 2; sexual y autolesión en 4) y la lista no está vacía.
public sealed class ContentModerationConfigurationTests
{
    [Fact]
    public void Thresholds_and_blocklist_come_from_appsettings()
    {
        using var factory = new UserResourceFactory();
        var options = factory.Services.GetRequiredService<ContentModerationOptions>();

        Assert.Equal(2, options.ThresholdFor("Violence"));
        Assert.Equal(2, options.ThresholdFor("Hate"));
        Assert.Equal(4, options.ThresholdFor("Sexual"));
        Assert.Equal(4, options.ThresholdFor("SelfHarm"));
        Assert.Contains("pendejo", options.Blocklist);
        Assert.Contains("te voy a matar", options.Blocklist);
    }
}
