using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

public static class LoginConfiguration
{
    public static void AddLogin(
        this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<LoginService>();

        var configuredJwt =
            builder.Configuration
                .GetSection("Jwt")
                .Get<JwtOptions>() ??
            new JwtOptions();

        var jwt = new JwtOptions
        {
            Enabled = configuredJwt.Enabled,
            Issuer = configuredJwt.Issuer,
            Audience = configuredJwt.Audience,
            AccessTokenMinutes =
                configuredJwt.AccessTokenMinutes,
            ExpirationSeconds =
                configuredJwt.ExpirationSeconds,
            ClockSkewSeconds =
                configuredJwt.ClockSkewSeconds,

            // El secreto de U2 solo se obtiene desde configuración
            // externa, principalmente JWT_SIGNING_KEY.
            SigningKey =
                builder.Configuration["JWT_SIGNING_KEY"]
        };

        if (jwt.Enabled)
        {
            if (!jwt.IsValid())
            {
                throw Invalid("Jwt");
            }

            builder.Services.AddSingleton(jwt);

            builder.Services.AddSingleton<IJwtTokenService>(
                provider =>
                    new JwtTokenService(
                        jwt,
                        provider.GetRequiredService<TimeProvider>()));
        }
        else
        {
            builder.Services
                .AddSingleton<IJwtTokenService,
                    UnavailableJwtTokenService>();
        }

        var rateLimits =
            builder.Configuration
                .GetSection("LoginRateLimit")
                .Get<RateLimitOptions>() ??
            new RateLimitOptions();

        if (!rateLimits.IsValid())
        {
            throw Invalid("LoginRateLimit");
        }

        builder.Services.AddSingleton(rateLimits);

        var redis =
            builder.Configuration
                .GetSection("Redis")
                .Get<RedisOptions>() ??
            new RedisOptions();

        ConfigurationOptions? redisConfiguration = null;

        if (redis.Enabled)
        {
            try
            {
                redisConfiguration = RedisAccess.Build(redis);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    or FormatException)
            {
                throw Invalid("Redis");
            }
        }

        builder.Services.AddSingleton<IRateLimitStore>(
            _ => new RedisRateLimitStore(redisConfiguration));
    }

    private static OptionsValidationException Invalid(
        string section) =>
        new(
            Options.DefaultName,
            typeof(LoginConfiguration),
            [$"Configuración inválida en {section}; no se muestran valores."]);
}
