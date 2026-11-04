using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-08: fábrica para University Catalog. Reemplaza el repositorio
// MongoDB y el rate limit por fakes compartidos (sin Docker,
// Testcontainers ni MongoDB real). El seed queda deshabilitado por
// defecto (UniversitySeedOptions.Enabled = false).
public sealed class UniversityFactory(
    UniversityTestStore? universities = null,
    LogoutTestRateLimitStore? rateLimit = null)
    : FoundationFactory()
{
    public UniversityTestStore Universities { get; } =
        universities ?? new();

    public LogoutTestRateLimitStore RateLimit { get; } =
        rateLimit ?? new();

    public int RequestsPerWindow { get; set; } =
        UniversityRateLimitOptions.DefaultRequestsPerWindow;

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUniversityRepository>();
            services.RemoveAll<IRateLimitStore>();
            services.RemoveAll<UniversityRateLimitOptions>();

            services.AddSingleton<IUniversityRepository>(Universities);
            services.AddSingleton<IRateLimitStore>(RateLimit);
            services.AddSingleton(
                new UniversityRateLimitOptions
                {
                    RequestsPerWindow = RequestsPerWindow
                });
        });
    }
}

// Replica la semántica del MongoUniversityRepository:
//  - listado estable `name ASC` y luego `id ASC`;
//  - upsert por `code` que conserva Id/CreatedAt existentes;
//  - inserción con Id determinista generado por el store.
public sealed class UniversityTestStore : IUniversityRepository
{
    public List<University> Universities { get; } = [];

    public bool Unavailable { get; set; }

    private int nextSequence;

    private string NextIdRaw() =>
        $"0123456789abcd{nextSequence++:D10}";
    public Task<(
        IReadOnlyList<University> Items,
        long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        var ordered = Universities
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(
            ((IReadOnlyList<University>)items, (long)ordered.Count));
    }

    public Task<University?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(
            Universities.FirstOrDefault(
                item => item.Id == id));
    }

    public Task UpsertSeedAsync(
        IReadOnlyCollection<University> universities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        foreach (var university in universities)
        {
            var existing = Universities.FirstOrDefault(
                candidate => candidate.Code == university.Code);

            if (existing is null)
            {
                Universities.Add(
                    new University
                    {
                        Id = $"0123456789abcd{nextSequence++:D10}",
                        Code = university.Code,
                        Name = university.Name,
                        City = university.City,
                        Country = university.Country,
                        CreatedAt = university.CreatedAt
                    });

                continue;
            }

            // Upsert: actualiza por `code`, conserva Id/CreatedAt.
            Universities.Remove(existing);

            Universities.Add(
                new University
                {
                    Id = existing.Id,
                    Code = existing.Code,
                    Name = university.Name,
                    City = university.City,
                    Country = university.Country,
                    CreatedAt = existing.CreatedAt
                });
        }

        return Task.CompletedTask;
    }
}
