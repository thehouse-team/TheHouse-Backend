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

    public static string LandlordRead(string userId) =>
        "thehouse:u2:landlord:read:v1:" + Hash(userId);

    public static string LandlordWrite(string userId) =>
        "thehouse:u2:landlord:write:v1:" + Hash(userId);

    public static string RoomGenerate(string userId) =>
        "thehouse:u2:landlord:generate:v1:" + Hash(userId);

    public static string AdminUser(string userId) =>
        "thehouse:u2:admin:user:v1:" + Hash(userId);

    public static string ReportsUser(string userId) =>
        "thehouse:u2:reports:user:v1:" + Hash(userId);

    public static string ReportCreatesUser(string userId) =>
        "thehouse:u2:reports:create:v1:" + Hash(userId);

    public static string SearchIp(string ipAddress) =>
        "thehouse:u2:search:ip:v1:" + Hash(ipAddress);

    public static string RoomDetailUser(string userId) =>
        "thehouse:u2:search:detail:v1:" + Hash(userId);

    // NV-16: solicitudes y chat por usuario; consultas por usuario y conversación.
    public static string MessagingUser(string userId) =>
        "thehouse:u2:messaging:user:v1:" + Hash(userId);

    public static string RequestCreatesUser(string userId) =>
        "thehouse:u2:messaging:request:v1:" + Hash(userId);

    public static string MessageSendsUser(string userId) =>
        "thehouse:u2:messaging:send:v1:" + Hash(userId);

    public static string MessagePolls(string userId, string conversationId) =>
        "thehouse:u2:messaging:poll:v1:" + Hash(userId + ":" + conversationId);

    public static string MessagingLock(string userId) =>
        "thehouse:u2:messaging:lock:v1:" + Hash(userId);

    // NV-18: convenios por usuario; código de cierre por usuario, por IP y
    // por cuenta en 15 minutos; candado por cuarto al aceptar o terminar.
    public static string RentalsUser(string userId) =>
        "thehouse:u2:rentals:user:v1:" + Hash(userId);

    public static string ClosingCodeUser(string userId) =>
        "thehouse:u2:rentals:code:user:v1:" + Hash(userId);

    public static string ClosingCodeIp(string ipAddress) =>
        "thehouse:u2:rentals:code:ip:v1:" + Hash(ipAddress);

    public static string ClosingCodeAttempts(string userId) =>
        "thehouse:u2:rentals:code:attempts:v1:" + Hash(userId);

    public static string RentalRoomLock(string roomId) =>
        "thehouse:u2:rentals:lock:room:v1:" + Hash(roomId);

    // NV-20: bandeja y contador de notificaciones por usuario.
    public static string NotificationsUser(string userId) =>
        "thehouse:u2:notifications:user:v1:" + Hash(userId);

    public static string NotificationCountUser(string userId) =>
        "thehouse:u2:notifications:count:v1:" + Hash(userId);

    public static string PropertyIp(string ipAddress) =>
        "thehouse:u2:properties:ip:v1:" + Hash(ipAddress);

    public static string UniversityIp(string ipAddress) =>
        "thehouse:u2:universities:ip:v1:" + Hash(ipAddress);

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))
        .ToLowerInvariant();
}
