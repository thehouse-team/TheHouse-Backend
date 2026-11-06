using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

public sealed class SeedCommandPolicyTests
{
    private const string Universities = SeedCommandPolicy.SeedUniversities;
    private const string Properties = SeedCommandPolicy.SeedProperties;
    private const string Allow = SeedCommandPolicy.AllowProductionSeed;

    [Fact]
    public void Production_seed_is_rejected_without_explicit_flag() =>
        Assert.Equal(
            SeedCommandDecision.ProductionNotAllowed,
            SeedCommandPolicy.Evaluate([Universities, Properties], true, true, isProduction: true));

    [Fact]
    public void Production_seed_runs_only_with_explicit_flag() =>
        Assert.Equal(
            SeedCommandDecision.RunInProduction,
            SeedCommandPolicy.Evaluate([Universities, Properties, Allow], true, true, isProduction: true));

    [Fact]
    public void Flag_does_not_bypass_disabled_seed_options()
    {
        Assert.Equal(
            SeedCommandDecision.UniversitySeedDisabled,
            SeedCommandPolicy.Evaluate([Universities, Allow], false, true, isProduction: true));
        Assert.Equal(
            SeedCommandDecision.PropertySeedDisabled,
            SeedCommandPolicy.Evaluate([Properties, Allow], true, false, isProduction: true));
    }

    [Fact]
    public void Flag_alone_does_not_request_a_seed() =>
        Assert.Equal(
            SeedCommandDecision.NotRequested,
            SeedCommandPolicy.Evaluate([Allow], true, true, isProduction: true));

    [Fact]
    public void Non_production_seed_runs_without_flag() =>
        Assert.Equal(
            SeedCommandDecision.Run,
            SeedCommandPolicy.Evaluate([Universities], true, false, isProduction: false));

    [Theory]
    [InlineData("--ALLOW-PRODUCTION-SEED")]
    [InlineData("--allow-production-seed=true")]
    [InlineData("allow-production-seed")]
    public void Only_the_exact_flag_authorizes_production(string lookalike) =>
        Assert.Equal(
            SeedCommandDecision.ProductionNotAllowed,
            SeedCommandPolicy.Evaluate([Universities, lookalike], true, true, isProduction: true));
}
