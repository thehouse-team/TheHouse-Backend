using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using TheHouse.Application.Abstractions.Health;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Health;

namespace TheHouse.Configuration;

public static class FoundationConfiguration
{
    public static void AddFoundationConfiguration(this WebApplicationBuilder builder)
    {
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        // No habilitar el startup filter permisivo de ASPNETCORE_FORWARDEDHEADERS_ENABLED.
        builder.WebHost.UseSetting("forwardedHeaders_enabled", "false");

        var limits = AddValidated<FoundationOptions>(builder, "Foundation", _ => true);
        var proxy = AddValidated<ProxyOptions>(builder, "Proxy", options => options.IsValid());
        AddValidated<MongoOptions>(builder, "MongoDB", options => options.IsValid());
        AddValidated<RedisOptions>(builder, "Redis", options => options.IsValid());
        AddValidated<JwtOptions>(builder, "Jwt", options => options.IsValid());

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = limits.MaxRequestBodyBytes;
            options.Limits.MaxRequestLineSize = 8192;
            options.Limits.MaxRequestHeadersTotalSize = 16384;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = proxy.Enabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = proxy.ForwardLimit;
            options.RequireHeaderSymmetry = true;
            // Solo se eliminan defaults cuando existe una allowlist explícita válida.
            if (proxy.Enabled)
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var address in proxy.KnownProxies)
                    options.KnownProxies.Add(IPAddress.Parse(address));
                foreach (var network in proxy.KnownNetworks)
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        builder.Services.AddSingleton<IMongoHealthProbe, UnconfiguredMongoHealthProbe>();
        builder.Services.AddSingleton<IRedisHealthProbe, UnconfiguredRedisHealthProbe>();
    }

    private static T AddValidated<T>(WebApplicationBuilder builder, string section, Func<T, bool> valid)
        where T : class, new()
    {
        // Validación antes de Build; sin inner exceptions ni valores del binder en diagnósticos.
        var value = new T();
        try
        {
            builder.Configuration.GetSection(section).Bind(value,
                options => options.ErrorOnUnknownConfiguration = true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or FormatException)
        {
            throw Invalid<T>(section);
        }

        if (!Validator.TryValidateObject(value, new ValidationContext(value), [], true) || !valid(value))
            throw Invalid<T>(section);

        builder.Services.AddOptions<T>().Configure(options =>
        {
            // Las opciones son un snapshot de arranque; no hay reload parcial inseguro.
            foreach (var property in typeof(T).GetProperties().Where(property => property.CanWrite))
                property.SetValue(options, property.GetValue(value));
        }).Validate(options => Validator.TryValidateObject(options, new ValidationContext(options), [], true) && valid(options),
            $"Configuración inválida en {section}; no se muestran valores.").ValidateOnStart();
        return value;
    }

    private static OptionsValidationException Invalid<T>(string section) =>
        new(Options.DefaultName, typeof(T), [$"Configuración inválida en {section}; no se muestran valores."]);
}
