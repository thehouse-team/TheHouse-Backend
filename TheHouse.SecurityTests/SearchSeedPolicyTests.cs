using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

// NV-14: los seeds de colonias y de demostración siguen las reglas de M-BE-043.
public sealed class SearchSeedPolicyTests
{
    [Fact]
    public void Seeds_require_configuration_and_the_production_flag()
    {
        Assert.NotNull(SearchConfiguration.Reject(["--seed-places"], placesEnabled: false, demoEnabled: true, isProduction: false));
        Assert.NotNull(SearchConfiguration.Reject(["--seed-demo-rooms"], placesEnabled: true, demoEnabled: false, isProduction: false));
        Assert.Null(SearchConfiguration.Reject(["--seed-places"], placesEnabled: true, demoEnabled: false, isProduction: false));
        Assert.NotNull(SearchConfiguration.Reject(["--seed-places"], placesEnabled: true, demoEnabled: false, isProduction: true));
        Assert.Null(SearchConfiguration.Reject(["--seed-places", "--allow-production-seed"], placesEnabled: true, demoEnabled: false, isProduction: true));
    }
}
