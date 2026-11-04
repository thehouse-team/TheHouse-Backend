namespace TheHouse.Application.Common;

public static class RevocationKeys
{
    public const string Prefix =
        "thehouse:u2:auth:revoked:jti:v1:";

    public const string JtiClaim = "jti";

    public const string ExpClaim = "exp";

    public const int CanonicalJtiLength = 32;

    public static string ForJti(
        string jti) =>
        Prefix + jti;

    public static bool IsCanonicalJti(
        string? jti) =>
        jti is not null &&
        jti.Length == CanonicalJtiLength &&
        jti.All(IsHexDigit);

    private static bool IsHexDigit(
        char value) =>
        value is
            (>= '0' and <= '9') or
            (>= 'a' and <= 'f');
}
