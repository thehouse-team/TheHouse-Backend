using System.Net;
using StackExchange.Redis;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.Configuration;

// Construcción compartida de la configuración de conexión Redis
// con las garantías hardening de U2: solo loopback sin TLS explícito
// no excluido, fail-fast, timeouts acotados, AllowAdmin deshabilitado.
internal static class RedisAccess
{
    public static ConfigurationOptions Build(RedisOptions redis)
    {
        var configuration =
            ConfigurationOptions.Parse(
                redis.ConnectionString!);

        if (!configuration.Ssl &&
            configuration.EndPoints.Any(endpoint =>
                endpoint switch
                {
                    DnsEndPoint dns =>
                        !IsLoopback(dns.Host),

                    IPEndPoint ip =>
                        !IPAddress.IsLoopback(ip.Address),

                    _ => true
                }))
        {
            throw new ArgumentException();
        }

        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = 0;
        configuration.ConnectTimeout =
            redis.TimeoutMilliseconds;
        configuration.AsyncTimeout =
            redis.TimeoutMilliseconds;
        configuration.SyncTimeout =
            redis.TimeoutMilliseconds;
        configuration.BacklogPolicy =
            BacklogPolicy.FailFast;
        configuration.AllowAdmin = false;

        return configuration;
    }

    private static bool IsLoopback(string host) =>
        host.Equals(
            "localhost",
            StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out var ip) &&
         IPAddress.IsLoopback(ip));
}
