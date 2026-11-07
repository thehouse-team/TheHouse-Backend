using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.IntegrationTests;

// NV-07: fábrica para Property Core. Reemplaza el repositorio
// MongoDB y el rate limit por fakes compartidos (sin Docker/
// Testcontainers). Comparte `LogoutTestRateLimitStore` con NV-06
// para demostrar límite compartido entre réplicas/endpoints.
public sealed class PropertyFactory(
    PropertyTestStore? properties = null,
    LogoutTestRateLimitStore? rateLimit = null)
    : FoundationFactory()
{
    public PropertyTestStore Properties { get; } =
        properties ?? new();

    public LogoutTestRateLimitStore RateLimit { get; } =
        rateLimit ?? new();

    public int RequestsPerWindow { get; set; } =
        PropertyRateLimitOptions.DefaultRequestsPerWindow;

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPropertyRepository>();
            services.RemoveAll<IRateLimitStore>();
            services.RemoveAll<PropertyRateLimitOptions>();

            services.AddSingleton<IPropertyRepository>(Properties);
            services.AddSingleton<IRateLimitStore>(RateLimit);
            services.AddSingleton(
                new PropertyRateLimitOptions
                {
                    RequestsPerWindow = RequestsPerWindow
                });
        });
    }
}

public sealed class PropertyTestStore : IPropertyRepository
{
    public List<Property> Properties { get; } = [];

    public bool Unavailable { get; set; }

    public Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        var ordered = Properties
            .OrderByDescending(property => property.CreatedAt)
            .ThenByDescending(property => property.Id, StringComparer.Ordinal)
            .ToList();

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(
            ((IReadOnlyList<Property>)items, (long)ordered.Count));
    }

    public Task<Property?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult(
            Properties.FirstOrDefault(
                property => property.Id == id));
    }

    public Task<IReadOnlyList<Property>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        return Task.FromResult<IReadOnlyList<Property>>(
            Properties.Where(property => ids.Contains(property.Id)).ToList());
    }

    public Task UpsertSeedAsync(
        IReadOnlyCollection<Property> properties,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Unavailable)
        {
            throw new DependencyUnavailableException();
        }

        foreach (var property in properties)
        {
            var existing = Properties.FirstOrDefault(
                candidate => candidate.Id == property.Id);

            if (existing is null)
            {
                Properties.Add(property);
                continue;
            }

            // Upsert por `_id`: actualiza los campos del seed y
            // conserva el `createdAt` original.
            Properties.Remove(existing);

            Properties.Add(
                new Property
                {
                    Id = existing.Id,
                    Title = property.Title,
                    Description = property.Description,
                    Price = property.Price,
                    Currency = property.Currency,
                    City = property.City,
                    UniversityId = property.UniversityId,
                    Bedrooms = property.Bedrooms,
                    Bathrooms = property.Bathrooms,
                    AreaM2 = property.AreaM2,
                    Amenities = property.Amenities,
                    CreatedAt = existing.CreatedAt
                });
        }

        return Task.CompletedTask;
    }
}
