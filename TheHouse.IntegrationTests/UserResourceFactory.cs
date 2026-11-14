using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;

namespace TheHouse.IntegrationTests;

// NV-15/NV-17: fábrica con dos usuarios reales (login por HTTP) y
// almacenes en memoria para MongoDB y Redis. Permite pruebas BOLA.
public sealed class UserResourceFactory(UserRateLimitOptions? limits = null)
    : FoundationFactory(Settings)
{
    public const string Password = "Correct-Horse-Battery-9";
    public const string AliceId = "665f1c2a9b1e4a00000a11ce";
    public const string BobId = "665f1c2a9b1e4a0000000b0b";
    public const string CarolId = "665f1c2a9b1e4a0000000ca1";
    public const string PropertyA = "0123456789876543000000a1";
    public const string PropertyB = "0123456789876543000000b2";
    public const string PropertyC = "0123456789876543000000c3";

    private static readonly IReadOnlyDictionary<string, string?> Settings =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Jwt:Enabled"] = "true",
            ["JWT_SIGNING_KEY"] = LogoutFactory.SigningKey,
            ["Jwt:Issuer"] = "thehouse-api",
            ["Jwt:Audience"] = "thehouse-clients",
            ["Jwt:ExpirationSeconds"] = "900",
            ["Jwt:ClockSkewSeconds"] = "30",
            ["LoginRateLimit:LoginEmailPerWindow"] = "5",
            ["LoginRateLimit:LoginIpPerWindow"] = "20",
            ["LoginRateLimit:WindowSeconds"] = "60",
            ["LogoutRateLimit:LogoutIpPerWindow"] = "30",
            ["LogoutRateLimit:WindowSeconds"] = "60",
        };

    public MultiUserStore Users { get; } = new();

    public LogoutTestRateLimitStore RateLimit { get; } = new();

    public LogoutTestRevocationStore Revocation { get; } = new();

    public PropertyTestStore Properties { get; } = Seeded();

    public InMemoryFavoriteRepository Favorites { get; } = new();

    public InMemoryVisitRepository Visits { get; } = new();

    public InMemoryUserLock Lock { get; } = new();

    public InMemoryProfileRepository Profiles { get; } = new();

    public InMemoryPreferencesRepository PreferencesStore { get; } = new();

    public InMemoryLandlordProfileRepository Landlords { get; } = new();

    public InMemoryBuildingRepository Buildings { get; } = new();

    public InMemoryRoomRepository Rooms { get; } = new();

    public InMemoryPlaceRepository Places { get; } = new();

    public InMemoryRentalRequestRepository RentalRequests { get; } = new();

    public InMemoryConversationRepository Conversations { get; } = new();

    public InMemoryAgreementRepository Agreements { get; } = new();

    public InMemoryNotificationRepository Notifications { get; } = new();

    public InMemoryReviewRepository Reviews { get; } = new();

    // NV-11: el catálogo real del seed (10 universidades con coordenadas).
    public UniversityTestStore Universities { get; } = SeededUniversities();

    public System.Collections.Concurrent.ConcurrentQueue<string> Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IRateLimitStore>();
            services.RemoveAll<ITokenRevocationStore>();
            services.RemoveAll<IRefreshTokenStore>();
            services.RemoveAll<ISessionRevocationStore>();
            services.RemoveAll<IPropertyRepository>();
            services.RemoveAll<IFavoriteRepository>();
            services.RemoveAll<IVisitRepository>();
            services.RemoveAll<IUserLock>();
            services.RemoveAll<IProfileRepository>();
            services.RemoveAll<IPreferencesRepository>();
            services.RemoveAll<IUniversityRepository>();
            services.RemoveAll<ILandlordProfileRepository>();
            services.RemoveAll<IBuildingRepository>();
            services.RemoveAll<IRoomRepository>();
            services.RemoveAll<IPlaceRepository>();
            services.RemoveAll<IRentalRequestRepository>();
            services.RemoveAll<IConversationRepository>();
            services.RemoveAll<IAgreementRepository>();
            services.RemoveAll<INotificationRepository>();
            services.RemoveAll<IReviewRepository>();

            services.AddSingleton<IUserRepository>(Users);
            services.AddSingleton<IRateLimitStore>(RateLimit);
            services.AddSingleton<ITokenRevocationStore>(Revocation);
            services.AddSingleton<IRefreshTokenStore>(new InMemoryRefreshTokenStore());
            services.AddSingleton<ISessionRevocationStore>(new InMemorySessionRevocationStore());
            services.AddSingleton<IPropertyRepository>(Properties);
            services.AddSingleton<IFavoriteRepository>(Favorites);
            services.AddSingleton<IVisitRepository>(Visits);
            services.AddSingleton<IUserLock>(Lock);
            services.AddSingleton<IProfileRepository>(Profiles);
            services.AddSingleton<IPreferencesRepository>(PreferencesStore);
            services.AddSingleton<IUniversityRepository>(Universities);
            services.AddSingleton<ILandlordProfileRepository>(Landlords);
            services.AddSingleton<IBuildingRepository>(Buildings);
            services.AddSingleton<IRoomRepository>(Rooms);
            services.AddSingleton<IPlaceRepository>(Places);
            services.AddSingleton<IRentalRequestRepository>(RentalRequests);
            services.AddSingleton<IConversationRepository>(Conversations);
            services.AddSingleton<IAgreementRepository>(Agreements);
            services.AddSingleton<INotificationRepository>(Notifications);
            services.AddSingleton<IReviewRepository>(Reviews);
            services.AddSingleton<ILoggerProvider>(new CaptureProvider(Logs));

            if (limits is not null)
            {
                services.RemoveAll<UserRateLimitOptions>();
                services.AddSingleton(limits);
            }
        });
    }

    // Devuelve un cliente HTTPS con el Bearer del usuario ya puesto.
    public async Task<HttpClient> ClientForAsync(string userId)
    {
        var hasher = Services.GetRequiredService<IPasswordHasher>();
        var email = userId switch
        {
            AliceId => "alice@example.com",
            BobId => "bob@example.com",
            _ => "carol@example.com",
        };
        Users.Add(new User
        {
            Id = userId,
            Email = email,
            PasswordHash = hasher.Hash(Password),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var client = CreateHttpsClient();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            document.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    // Horario válido: `days` días adelante a la hora local indicada en
    // America/Mexico_City (UTC-06:00, sin horario de verano).
    public static string Slot(int days, int hour, int minute = 0)
    {
        var localToday = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
        return $"{localToday.AddDays(days):yyyy-MM-dd}T{hour:00}:{minute:00}:00-06:00";
    }

    private static UniversityTestStore SeededUniversities()
    {
        var store = new UniversityTestStore();
        store.UpsertSeedAsync(UniversitySeedData.Create(), CancellationToken.None).GetAwaiter().GetResult();
        return store;
    }

    private static PropertyTestStore Seeded()
    {
        var store = new PropertyTestStore();
        foreach (var (id, title) in new[] { (PropertyA, "Depa A"), (PropertyB, "Cuarto B"), (PropertyC, "Casa C") })
        {
            store.Properties.Add(new Property
            {
                Id = id,
                Title = title,
                Description = "Descripción con dirección exacta que no debe salir.",
                Price = 3500m,
                Currency = "MXN",
                City = "Tula de Allende",
                UniversityId = "UTTT",
                Bedrooms = 1,
                Bathrooms = 1,
                AreaM2 = 30,
                CreatedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            });
        }

        return store;
    }
}

public sealed class MultiUserStore : IUserRepository
{
    private readonly ConcurrentUsers users = new();

    public void Add(User user) => users[user.Id] = user;

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(users.Values.FirstOrDefault(user => user.Email == normalizedEmail));

    public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(users.GetValueOrDefault(id));

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken)
    {
        users[user.Id] = user;
        return Task.FromResult(user);
    }

    private sealed class ConcurrentUsers : System.Collections.Concurrent.ConcurrentDictionary<string, User>
    {
        public ConcurrentUsers()
            : base(StringComparer.Ordinal)
        {
        }
    }
}

internal sealed class CaptureProvider(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);

    public void Dispose()
    {
    }

    private sealed class CaptureLogger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
    }
}
