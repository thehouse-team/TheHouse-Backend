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

public sealed class LoginFactory(
    LoginTestStore? store = null,
    LoginTestRateLimitStore? rateLimit = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    InMemoryRefreshTokenStore? refreshTokens = null,
    InMemorySessionRevocationStore? sessionRevocation = null)
    : FoundationFactory(BuildSettings(settings))
{
    public const string SigningKey =
        "0123456789abcdef0123456789abcdef";

    public LoginTestStore Store { get; } =
        store ?? new();

    public LoginTestRateLimitStore RateLimit { get; } =
        rateLimit ?? new();

    public ConcurrentQueue<string> Logs { get; } = new();

    // NV-10: almacenes de sesión compartibles entre fábricas para
    // simular varias réplicas sobre el mismo MongoDB/Redis.
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

            services.AddSingleton<IUserRepository>(Store);
            services.AddSingleton<IRateLimitStore>(RateLimit);
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
                ["LoginRateLimit:WindowSeconds"] = "60"
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

public sealed class LoginTestStore : IUserRepository
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

public sealed class LoginTestRateLimitStore : IRateLimitStore
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
