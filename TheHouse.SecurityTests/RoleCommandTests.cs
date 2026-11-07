using TheHouse.Configuration;

namespace TheHouse.SecurityTests;

// NV-21: los roles solo se asignan por línea de comandos y, en Production,
// solo con la bandera explícita.
public sealed class RoleCommandTests
{
    [Fact]
    public void Parse_reads_grant_and_revoke_with_email_and_role()
    {
        var grant = RoleCommand.Parse(["--grant-role", "ana@example.com", "admin"]);
        var revoke = RoleCommand.Parse(["--revoke-role", "ana@example.com", "reviewer", "--allow-production-role-change"]);

        Assert.Equal(new RoleCommand(true, "ana@example.com", "admin"), grant);
        Assert.Equal(new RoleCommand(false, "ana@example.com", "reviewer"), revoke);
        Assert.Null(RoleCommand.Parse(["--seed-universities"]));
        Assert.Equal(new RoleCommand(true, null, null), RoleCommand.Parse(["--grant-role"]));
    }

    [Fact]
    public void Production_requires_the_explicit_flag()
    {
        Assert.True(RoleCommand.Allowed(["--grant-role", "a@example.com", "admin"], isProduction: false));
        Assert.False(RoleCommand.Allowed(["--grant-role", "a@example.com", "admin"], isProduction: true));
        Assert.True(RoleCommand.Allowed(["--grant-role", "a@example.com", "admin", RoleCommand.AllowProductionRoleChange], isProduction: true));
    }
}
