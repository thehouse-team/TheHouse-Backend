using System.Security.Cryptography;
using System.Text;

namespace TheHouse.Application.Common;

public static class RateLimitKeys
{
    public static string LoginIp(string ipAddress) =>
        "thehouse:u2:login:ip:v1:" + Hash(ipAddress);

    public static string LoginEmail(
        string ipAddress,
        string normalizedEmail) =>
        "thehouse:u2:login:email:v1:" +
        Hash(ipAddress + "\n" + normalizedEmail);

    public static string LogoutIp(string ipAddress) =>
        "thehouse:u2:logout:ip:v1:" + Hash(ipAddress);

    public static string RefreshIp(string ipAddress) =>
        "thehouse:u2:refresh:ip:v1:" + Hash(ipAddress);

    public static string FavoritesUser(string userId) =>
        "thehouse:u2:favorites:user:v1:" + Hash(userId);

    public static string VisitsUser(string userId) =>
        "thehouse:u2:visits:user:v1:" + Hash(userId);

    public static string VisitCreatesUser(string userId) =>
        "thehouse:u2:visits:create:v1:" + Hash(userId);

    public static string VisitLock(string userId) =>
        "thehouse:u2:visits:lock:v1:" + Hash(userId);

    public static string ProfileUser(string userId) =>
        "thehouse:u2:profile:user:v1:" + Hash(userId);

    public static string UniversitySearchIp(string ipAddress) =>
        "thehouse:u2:universities:search:ip:v1:" + Hash(ipAddress);

    public static string PropertyIp(string ipAddress) =>
        "thehouse:u2:properties:ip:v1:" + Hash(ipAddress);

    public static string UniversityIp(string ipAddress) =>
        "thehouse:u2:universities:ip:v1:" + Hash(ipAddress);

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();
}
