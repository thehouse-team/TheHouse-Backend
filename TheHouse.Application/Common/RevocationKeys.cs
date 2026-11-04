namespace TheHouse.Application.Common;

public static class RevocationKeys
{
    public const string Prefix = "auth:revoked:jti:";
    public const string JtiClaim = "jti";
    public const string ExpClaim = "exp";
    public const int CanonicalJtiLength = 32;

    // El `jti` proviene de un JWT validado; además solo se aceptan
    // identificadores hexadecimales canónicos (32 caracteres) para
    // impedir cualquier manipulación de la clave.
    public static string ForJti(string jti) => Prefix + jti;

    public static bool IsCanonicalJti(string? jti) =>
        jti is not null &&
        jti.Length == CanonicalJtiLength &&
        jti.All(IsHexDigit);

    private static bool IsHexDigit(char value) =>
        value is (>= '0' and <= '9') or (>= 'a' and <= 'f');
}
