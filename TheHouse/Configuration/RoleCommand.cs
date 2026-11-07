using TheHouse.Application.UseCases;

namespace TheHouse.Configuration;

// NV-21: asignación de roles solo desde la línea de comandos del contenedor
// (`az containerapp exec`), nunca por la API. En Production exige la bandera
// explícita `--allow-production-role-change`; cada cambio queda en la
// bitácora con actor `cli`.
public sealed record RoleCommand(bool Grant, string? Email, string? Role)
{
    public const string GrantRole = "--grant-role";

    public const string RevokeRole = "--revoke-role";

    public const string AllowProductionRoleChange = "--allow-production-role-change";

    public const string Actor = "cli";

    public static RoleCommand? Parse(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (args[index] is GrantRole or RevokeRole)
            {
                return new RoleCommand(
                    args[index] == GrantRole,
                    index + 1 < args.Count ? args[index + 1] : null,
                    index + 2 < args.Count ? args[index + 2] : null);
            }
        }

        return null;
    }

    public static bool Allowed(IReadOnlyCollection<string> args, bool isProduction) =>
        !isProduction || args.Contains(AllowProductionRoleChange, StringComparer.Ordinal);

    public async Task<int> RunAsync(IServiceProvider services, ILogger logger)
    {
        var staff = services.GetRequiredService<StaffService>();
        try
        {
            var userId = Grant
                ? await staff.GrantByEmailAsync(Email, Role, Actor, CancellationToken.None)
                : await staff.RevokeByEmailAsync(Email, Role, Actor, CancellationToken.None);

            // Se registra el id, nunca el correo.
            logger.LogWarning("Rol {Role} {Action} para el usuario {UserId} desde la línea de comandos.", Role, Grant ? "asignado" : "retirado", userId);
            Console.WriteLine(Grant ? "Role granted." : "Role revoked.");
            return 0;
        }
        catch (Exception exception) when (exception is Application.Common.RequestValidationException or Domain.Exceptions.NotFoundException or Domain.Exceptions.ConflictException)
        {
            Console.Error.WriteLine("Role change rejected: check the email and the role (reviewer or admin).");
            return 2;
        }
    }
}
