using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

// NV-15/NV-17 1.2.0 (M-BE-043): la migración a cuartos solo corre habilitada
// por configuración y, en Production, con su bandera explícita. La bandera de
// los seeds no la autoriza.
public sealed class RoomMigrationPolicyTests
{
    [Theory]
    [InlineData(false, false, new string[0], true)]
    [InlineData(false, true, new[] { SearchConfiguration.AllowProductionMigration }, true)]
    [InlineData(true, true, new string[0], true)]
    [InlineData(true, true, new[] { "--allow-production-seed" }, true)]
    [InlineData(true, true, new[] { SearchConfiguration.AllowProductionMigration }, false)]
    [InlineData(true, false, new string[0], false)]
    public void Migration_requires_configuration_and_explicit_production_flag(bool enabled, bool isProduction, string[] flags, bool rejected)
    {
        var args = new[] { SearchConfiguration.MigrateToRooms }.Concat(flags).ToArray();

        var rejection = SearchConfiguration.RejectMigration(args, enabled, isProduction);

        Assert.Equal(rejected, rejection is not null);
        Assert.DoesNotContain("password", rejection ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
