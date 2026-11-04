using System.Diagnostics;

namespace TheHouse.Infrastructure.Observability;

public static class TelemetrySources
{
    public const string RedisName =
        "TheHouse.Redis";

    public static readonly ActivitySource Redis =
        new(RedisName);
}
