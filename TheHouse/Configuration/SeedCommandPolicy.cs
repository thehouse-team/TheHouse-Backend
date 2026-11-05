namespace TheHouse.Configuration;

public enum SeedCommandDecision
{
    NotRequested,
    Run,
    RunInProduction,
    UniversitySeedDisabled,
    PropertySeedDisabled,
    ProductionNotAllowed
}

// Reglas de los comandos de seed. En Production solo se permiten con la
// bandera explícita --allow-production-seed, que el host registra en el log;
// nunca se habilitan cambiando el nombre del entorno.
public static class SeedCommandPolicy
{
    public const string SeedUniversities = "--seed-universities";
    public const string SeedProperties = "--seed-properties";
    public const string AllowProductionSeed = "--allow-production-seed";

    public static SeedCommandDecision Evaluate(
        IReadOnlyCollection<string> args,
        bool universitySeedEnabled,
        bool propertySeedEnabled,
        bool isProduction)
    {
        var seedUniversities = args.Contains(SeedUniversities, StringComparer.Ordinal);
        var seedProperties = args.Contains(SeedProperties, StringComparer.Ordinal);

        if (!seedUniversities && !seedProperties)
            return SeedCommandDecision.NotRequested;

        if (seedUniversities && !universitySeedEnabled)
            return SeedCommandDecision.UniversitySeedDisabled;

        if (seedProperties && !propertySeedEnabled)
            return SeedCommandDecision.PropertySeedDisabled;

        if (!isProduction)
            return SeedCommandDecision.Run;

        return args.Contains(AllowProductionSeed, StringComparer.Ordinal)
            ? SeedCommandDecision.RunInProduction
            : SeedCommandDecision.ProductionNotAllowed;
    }
}
