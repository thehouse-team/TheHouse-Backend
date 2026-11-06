using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.IntegrationTests;

public sealed class RegistrationFactory(RegistrationTestStore? store = null, RegistrationTestQuota? quota = null,
    IReadOnlyDictionary<string, string?>? settings = null) : FoundationFactory(settings)
{
    public RegistrationTestStore Store { get; } = store ?? new();
    public RegistrationTestQuota Quota { get; } = quota ?? new();
    public ConcurrentQueue<string> Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRegistrationRateLimiter>();
            services.AddSingleton<IUserRepository>(Store);
            services.AddSingleton<IRegistrationRateLimiter>(Quota);
            services.AddSingleton<ILoggerProvider>(new CaptureProvider(Logs));
        });
    }

    private sealed class CaptureProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
        public void Dispose() { }
        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + (exception is null ? "" : exception.ToString()));
        }
    }
}

// Dobles aislados solo en pruebas HTTP; NO demuestran índices Mongo o atomicidad Redis.
public sealed class RegistrationTestStore : IUserRepository
{
    public ConcurrentDictionary<string, User> Users { get; } = new(StringComparer.Ordinal);
    public bool Unavailable { get; set; }
    public bool UnexpectedFailure { get; set; }

    public Task<User?> GetByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        Users.TryGetValue(normalizedEmail, out var user);
        return Task.FromResult<User?>(user);
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
    
        return Task.FromResult(Users.Values.FirstOrDefault(user => user.Id == id));
    }

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Unavailable) throw new DependencyUnavailableException();
        if (UnexpectedFailure) throw new InvalidOperationException("internal-collection-marker");
        var stored = new User { Id = "665f1c2a9b1e4a0012345678", Email = user.Email, PasswordHash = user.PasswordHash,
            CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt };
        if (!Users.TryAdd(stored.Email, stored)) throw new DuplicateEmailException();
        return Task.FromResult(stored);
    }
}

public sealed class RegistrationTestQuota : IRegistrationRateLimiter
{
    private readonly ConcurrentDictionary<string, int> counts = new(StringComparer.Ordinal);
    public bool Unavailable { get; set; }
    public Task<bool> AllowAsync(string clientAddress, CancellationToken cancellationToken)
    {
        if (Unavailable) throw new DependencyUnavailableException();
        return Task.FromResult(counts.AddOrUpdate(clientAddress, 1, (_, value) => value + 1) <= 10);
    }
}
