using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
namespace TheHouse.IntegrationTests;

// NV-06: fábrica autónoma que configura JWT, rate limit y el
// almacén de revocación reemplazado. Un almacén compartido entre
// instancias permite simular múltiples réplicas.
public sealed class LogoutFactory(
    LogoutTestRevocationStore? revocation = null,
    LogoutTestRateLimitStore? rateLimit = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    InMemoryRefreshTokenStore? refreshTokens = null,
    InMemorySessionRevocationStore? sessionRevocation = null)
    : FoundationFactory(BuildSettings(settings))
{
    public const string SigningKey =
        "0123456789abcdef0123456789abcdef";

    public LogoutTestStore Store { get; } = new();

    public LogoutTestRevocationStore Revocation { get; } =
        revocation ?? new();

    public LogoutTestRateLimitStore RateLimit { get; } =
        rateLimit ?? new();

    public ConcurrentQueue<string> Logs { get; } = new();

    public InMemoryRefreshTokenStore RefreshTokens { get; } =
        refreshTokens ?? new();

    public InMemorySessionRevocationStore SessionRevocation { get; } =
        sessionRevocation ?? new();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRateLimitStore>();
            services.RemoveAll<ITokenRevocationStore>();

            services.AddSingleton<IUserRepository>(Store);
            services.AddSingleton<IRateLimitStore>(RateLimit);
            services.AddSingleton<ITokenRevocationStore>(Revocation);
            services.RemoveAll<IRefreshTokenStore>();
            services.RemoveAll<ISessionRevocationStore>();
            services.AddSingleton<IRefreshTokenStore>(RefreshTokens);
            services.AddSingleton<ISessionRevocationStore>(SessionRevocation);

            services.AddSingleton<ILoggerProvider>(
                new CaptureProvider(Logs));
        });
    }

    private static IReadOnlyDictionary<string, string?>
        BuildSettings(
            IReadOnlyDictionary<string, string?>? settings)
    {
        var result =
            new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Jwt:Enabled"] = "true",
                ["JWT_SIGNING_KEY"] = SigningKey,
                ["Jwt:Issuer"] = "thehouse-api",
                ["Jwt:Audience"] = "thehouse-clients",
                ["Jwt:ExpirationSeconds"] = "900",
                ["Jwt:ClockSkewSeconds"] = "30",
                ["LoginRateLimit:LoginEmailPerWindow"] = "5",
                ["LoginRateLimit:LoginIpPerWindow"] = "20",
                ["LoginRateLimit:WindowSeconds"] = "60",
                ["LogoutRateLimit:LogoutIpPerWindow"] = "30",
                ["LogoutRateLimit:WindowSeconds"] = "60"
            };

        if (settings is not null)
        {
            foreach (var item in settings)
            {
                result[item.Key] = item.Value;
            }
        }

        return result;
    }

    private sealed class CaptureProvider(
        ConcurrentQueue<string> messages)
        : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) =>
            new CaptureLogger(messages);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(
            ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(
                    formatter(state, exception) +
                    (exception is null
                        ? ""
                        : exception.ToString()));
            }
        }
    }
}

public sealed class LogoutTestStore : IUserRepository
{
    public User? User { get; set; }

    public bool Unavailable { get; set; }

    public Task<User?> GetByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(
            User?.Email == normalizedEmail
                ? User
                : null);
    }

    public Task<User?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }
    
        return Task.FromResult(User?.Id == id ? User : null);
    }

    public Task<User> CreateAsync(
        User user,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        User = user;
        return Task.FromResult(user);
    }
}

// NV-06: almacén de revocación en memoria para pruebas y simulación
// multi-réplica (se comparte la instancia entre "réplicas").
public sealed class LogoutTestRevocationStore : ITokenRevocationStore
{
    private readonly ConcurrentDictionary<string, long> revoked =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, long> ttl =
        new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public bool UnavailableOnRevoke { get; set; }

    public IReadOnlyDictionary<string, long> Ttl => ttl;

    public Task RevokeAsync(
        string jti,
        TimeSpan ttlValue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable || UnavailableOnRevoke)
        {
            throw new DependencyUnavailableException();
        }

        revoked[jti] = 1;
        ttl[jti] = (long)ttlValue.TotalSeconds;

        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(
        string jti,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(revoked.ContainsKey(jti));
    }
}

public sealed class LogoutTestRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, long> counts =
        new(StringComparer.Ordinal);

    public bool Unavailable { get; set; }

    public Task<RateLimitDecision> ConsumeAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        var count = counts.AddOrUpdate(
            key,
            1,
            (_, current) => current + 1);

        return Task.FromResult(
            new RateLimitDecision(
                count <= limit,
                count,
                TimeSpan.FromSeconds(60)));
    }
}
