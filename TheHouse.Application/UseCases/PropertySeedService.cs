using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

public sealed class PropertySeedService(
    IPropertyRepository propertyRepository,
    IUniversityRepository universityRepository)
{
    public async Task SeedAsync(
        CancellationToken cancellationToken)
    {
        var (universities, _) =
            await universityRepository.ListAsync(
                1,
                UniversityQueryValidation.MaximumPageSize,
                cancellationToken);

        var universityIds = universities
            .Where(university =>
                !string.IsNullOrWhiteSpace(university.Code))
            .GroupBy(
                university => university.Code,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Id,
                StringComparer.Ordinal);

        var properties = PropertySeedData.Create()
            .Select(record =>
            {
                if (!universityIds.TryGetValue(
                        record.UniversityCode,
                        out var universityId))
                {
                    throw new InvalidOperationException(
                        $"Property seed requires university '{record.UniversityCode}'. Run the university seed first.");
                }

                return new Property
                {
                    Id = record.Id,
                    Title = record.Title,
                    Description = record.Description,
                    Price = record.Price,
                    Currency = record.Currency,
                    City = record.City,
                    UniversityId = universityId,
                    Bedrooms = record.Bedrooms,
                    Bathrooms = record.Bathrooms,
                    AreaM2 = record.AreaM2,
                    Amenities = record.Amenities,
                    CreatedAt = record.CreatedAt
                };
            })
            .ToArray();

        await propertyRepository.UpsertSeedAsync(
            properties,
            cancellationToken);
    }
}
