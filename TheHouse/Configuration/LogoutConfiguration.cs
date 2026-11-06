using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

public static class LogoutConfiguration
{
    public static void AddLogout(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<LogoutService>();

        var rateLimits =
            builder.Configuration
                .GetSection("LogoutRateLimit")
                .Get<LogoutRateLimitOptions>() ??
            new LogoutRateLimitOptions();

        if (!rateLimits.IsValid())
        {
            throw Invalid("LogoutRateLimit");
        }

        builder.Services.AddSingleton(rateLimits);

        var redis =
            builder.Configuration
                .GetSection("Redis")
                .Get<RedisOptions>() ??
            new RedisOptions();

        ConfigurationOptions? realConfiguration = null;

        if (redis.Enabled)
        {
            try
            {
                realConfiguration = RedisAccess.Build(redis);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or FormatException)
            {
                throw Invalid("Redis");
            }
        }

        // NV-10: una sola instancia (una conexión Redis) atiende la
        // revocación por `jti` y por sesión (`sid`).
        builder.Services.AddSingleton(
            _ => new RedisTokenRevocationStore(realConfiguration));
        builder.Services.AddSingleton<ITokenRevocationStore>(
            provider => provider.GetRequiredService<RedisTokenRevocationStore>());
        builder.Services.AddSingleton<ISessionRevocationStore>(
            provider => provider.GetRequiredService<RedisTokenRevocationStore>());
    }

    private static OptionsValidationException Invalid(
        string section) =>
        new(
            Options.DefaultName,
            typeof(LogoutConfiguration),
            [$"Configuración inválida en {section}; no se muestran valores."]);
}
