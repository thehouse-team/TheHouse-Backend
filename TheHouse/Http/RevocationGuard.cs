namespace TheHouse.Http;

// NV-06: marcador de convención que exige la consulta de revocación
// en cada solicitud autenticada. Los endpoints protegidos lo aplican
// mediante RequireTokenNotRevoked; el middleware central lo aplica.
public sealed class RevocationGuard
{
    public static readonly RevocationGuard Instance = new(optional: false);

    // NV-23: sesión opcional. Sin encabezado Authorization la solicitud pasa
    // como invitado; si llega un token se valida igual (inválido → 401).
    public static readonly RevocationGuard OptionalInstance = new(optional: true);

    private RevocationGuard(bool optional)
    {
        Optional = optional;
    }

    public bool Optional { get; }
}
