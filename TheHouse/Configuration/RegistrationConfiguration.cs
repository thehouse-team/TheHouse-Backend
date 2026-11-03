using System.Net;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Persistence.Mongo;
using TheHouse.Infrastructure.Security;

namespace TheHouse.Configuration;

public static class RegistrationConfiguration
{
    public static void AddRegistration(this WebApplicationBuilder builder)
    {
        var hashing = FoundationConfiguration.AddValidated<PasswordHashingOptions>(builder, "PasswordHashing", _ => true);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IPasswordHasher>(_ => new Pbkdf2PasswordHasher(hashing));
        builder.Services.AddSingleton<RegistrationService>();

        // Solo adaptadores de Registration. Deshabilitado significa 503, nunca persistencia/límite local.
        var mongo = builder.Configuration.GetSection("MongoDB").Get<MongoOptions>() ?? new();
        if (mongo.Enabled)
        {
            MongoClientSettings settings;
            try
            {
                settings = MongoClientSettings.FromConnectionString(mongo.ConnectionString);
                if (settings.AllowInsecureTls || (!settings.UseTls && settings.Servers.Any(server => !IsLoopback(server.Host))))
                    throw new ArgumentException();
                settings.ConnectTimeout = TimeSpan.FromSeconds(mongo.ConnectTimeoutSeconds);
                settings.ServerSelectionTimeout = settings.ConnectTimeout;
                settings.SocketTimeout = settings.ConnectTimeout;
                settings.MaxConnectionPoolSize = mongo.MaxPoolSize;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or MongoConfigurationException)
            {
                throw Invalid("MongoDB");
            }
            builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(settings));
            builder.Services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(mongo.Database));
            builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
        }
        else builder.Services.AddSingleton<IUserRepository, UnavailableUserRepository>();

        var redis = builder.Configuration.GetSection("Redis").Get<RedisOptions>() ?? new();
        ConfigurationOptions? redisConfiguration = null;
        if (redis.Enabled)
        {
            try
            {
                redisConfiguration = ConfigurationOptions.Parse(redis.ConnectionString!);
                if (!redisConfiguration.Ssl && redisConfiguration.EndPoints.Any(endpoint => endpoint switch
                    {
                        DnsEndPoint dns => !IsLoopback(dns.Host),
                        IPEndPoint ip => !IPAddress.IsLoopback(ip.Address),
                        _ => true
                    })) throw new ArgumentException();
                redisConfiguration.AbortOnConnectFail = false;
                redisConfiguration.ConnectRetry = 0;
                redisConfiguration.ConnectTimeout = redis.TimeoutMilliseconds;
                redisConfiguration.AsyncTimeout = redis.TimeoutMilliseconds;
                redisConfiguration.SyncTimeout = redis.TimeoutMilliseconds;
                redisConfiguration.BacklogPolicy = BacklogPolicy.FailFast;
                redisConfiguration.AllowAdmin = false;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                throw Invalid("Redis");
            }
        }
        builder.Services.AddSingleton<IRegistrationRateLimiter>(_ => new RedisRegistrationRateLimiter(redisConfiguration));
    }

    private static bool IsLoopback(string host) => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));

    private static OptionsValidationException Invalid(string section) => new(Options.DefaultName,
        typeof(RegistrationConfiguration), [$"Configuración inválida en {section}; no se muestran valores."]);
}
