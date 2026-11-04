using System.Net;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Health;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Health;
using TheHouse.Infrastructure.Persistence.Mongo;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

public static class RegistrationConfiguration
{
    public static void AddRegistration(
        this WebApplicationBuilder builder)
    {
        var hashing =
            FoundationConfiguration.AddValidated<
                PasswordHashingOptions>(
                builder,
                "PasswordHashing",
                _ => true);

        builder.Services.AddSingleton(
            TimeProvider.System);

        builder.Services.AddSingleton<IPasswordHasher>(
            _ => new Pbkdf2PasswordHasher(hashing));

        builder.Services.AddSingleton<
            RegistrationService>();

        ConfigureMongo(builder);
        ConfigureRedis(builder);
    }

    private static void ConfigureMongo(
        WebApplicationBuilder builder)
    {
        var mongo =
            builder.Configuration
                .GetSection("MongoDB")
                .Get<MongoOptions>()
            ?? new MongoOptions();

        if (!mongo.Enabled)
        {
            builder.Services.AddSingleton<
                IUserRepository,
                UnavailableUserRepository>();

            return;
        }

        MongoClientSettings settings;

        try
        {
            settings =
                MongoClientSettings
                    .FromConnectionString(
                        mongo.ConnectionString);

            if (settings.AllowInsecureTls ||
                (!settings.UseTls &&
                 settings.Servers.Any(
                     server =>
                         !IsLoopback(
                             server.Host))))
            {
                throw new ArgumentException();
            }

            settings.ConnectTimeout =
                TimeSpan.FromSeconds(
                    mongo.ConnectTimeoutSeconds);

            settings.ServerSelectionTimeout =
                settings.ConnectTimeout;

            settings.SocketTimeout =
                settings.ConnectTimeout;

            settings.MaxConnectionPoolSize =
                mongo.MaxPoolSize;
        }
        catch (Exception exception)
            when (exception is
                  ArgumentException or
                  FormatException or
                  MongoConfigurationException)
        {
            throw Invalid("MongoDB");
        }

        builder.Services.AddSingleton<IMongoClient>(
            _ => new MongoClient(settings));

        builder.Services.AddSingleton(
            provider =>
                provider
                    .GetRequiredService<IMongoClient>()
                    .GetDatabase(mongo.Database));

        builder.Services.AddSingleton<
            IUserRepository,
            MongoUserRepository>();

        // Foundation registra inicialmente un probe fail-closed.
        // Cuando MongoDB está habilitado, este registro posterior
        // es el adaptador real usado por DependencyReadinessCheck.
        builder.Services.AddSingleton<
            IMongoHealthProbe,
            MongoHealthProbe>();
    }

    private static void ConfigureRedis(
        WebApplicationBuilder builder)
    {
        var redis =
            builder.Configuration
                .GetSection("Redis")
                .Get<RedisOptions>()
            ?? new RedisOptions();

        ConfigurationOptions? configuration = null;

        if (redis.Enabled)
        {
            try
            {
                configuration =
                    RedisAccess.Build(redis);
            }
            catch (Exception exception)
                when (exception is
                      ArgumentException or
                      FormatException)
            {
                throw Invalid("Redis");
            }

            var healthConfiguration =
                configuration;

            builder.Services.AddSingleton<
                IRedisHealthProbe>(
                _ => new RedisHealthProbe(
                    healthConfiguration));
        }

        builder.Services.AddSingleton<
            IRegistrationRateLimiter>(
                _ =>
                    new RedisRegistrationRateLimiter(
                        configuration));
    }

    private static bool IsLoopback(
        string host) =>
        host.Equals(
            "localhost",
            StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(
             host,
             out var ip) &&
         IPAddress.IsLoopback(ip));

    private static OptionsValidationException Invalid(
        string section) =>
        new(
            Options.DefaultName,
            typeof(RegistrationConfiguration),
            [
                $"Configuración inválida en {section}; no se muestran valores."
            ]);
}
