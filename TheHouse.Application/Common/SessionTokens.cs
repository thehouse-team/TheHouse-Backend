using System.Security.Cryptography;
using System.Text;

namespace TheHouse.Application.Common;

// NV-10: generación y formato de refresh tokens e identificadores de
// sesión. El valor del refresh token son 32 bytes aleatorios en
// base64url (43 caracteres); solo se almacena su SHA-256.
public static class SessionTokens
{
    public const string SessionIdClaim = "sid";

    public const int RefreshTokenLength = 43;

    public const int CanonicalSessionIdLength = 32;

    public const string RevokedSessionPrefix =
        "thehouse:u2:auth:revoked:sid:v1:";

    public static string NewRefreshToken() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string NewSessionId() =>
        Convert
            .ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();

    public static string Hash(string refreshToken) =>
        Convert
            .ToHexString(
                SHA256.HashData(
                    Encoding.ASCII.GetBytes(refreshToken)))
            .ToLowerInvariant();

    public static bool IsWellFormedRefreshToken(
        string? value) =>
        value is not null &&
        value.Length == RefreshTokenLength &&
        value.All(IsBase64UrlChar);

    public static bool IsCanonicalSessionId(
        string? value) =>
        value is not null &&
        value.Length == CanonicalSessionIdLength &&
        value.All(character =>
            character is
                (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    public static string RevokedSessionKey(
        string sessionId) =>
        RevokedSessionPrefix + sessionId;

    private static bool IsBase64UrlChar(char value) =>
        value is
            (>= 'A' and <= 'Z') or
            (>= 'a' and <= 'z') or
            (>= '0' and <= '9') or
            '-' or '_';

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
