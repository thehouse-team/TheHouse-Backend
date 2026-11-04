namespace TheHouse.Http;

// NV-06: marcador de convención que exige la consulta de revocación
// en cada solicitud autenticada. Los endpoints protegidos lo aplican
// mediante RequireTokenNotRevoked; el middleware central lo aplica.
public sealed class RevocationGuard
{
    public static readonly RevocationGuard Instance = new();

    private RevocationGuard()
    {
    }
}
