using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Properties;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

public sealed class PropertyQueryService(
    IPropertyRepository propertyRepository)
{
    public async Task<PropertyListResponse> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(
            page,
            pageSize);

        var (items, totalItems) =
            await propertyRepository.ListAsync(
                page,
                pageSize,
                cancellationToken);

        var mapped = items
            .Select(static property =>
                new PropertyListItemResponse(
                    property.Id,
                    property.Title,
                    property.Price,
                    property.Currency,
                    property.City,
                    property.UniversityId,
                    property.Bedrooms,
                    property.Bathrooms,
                    property.AreaM2,
                    property.CreatedAt))
            .ToArray();

        return new PropertyListResponse(
            mapped,
            page,
            pageSize,
            totalItems,
            PropertyQueryValidation.CalculateTotalPages(
                totalItems,
                pageSize));
    }

    public async Task<PropertyDetailResponse> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!PropertyQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException(
                "id",
                "id must be a 24-character hexadecimal ObjectId.");
        }

        var property =
            await propertyRepository.GetByIdAsync(
                id,
                cancellationToken)
            ?? throw new NotFoundException("Property");

        return new PropertyDetailResponse(
            property.Id,
            property.Title,
            property.Description,
            property.Price,
            property.Currency,
            property.City,
            property.UniversityId,
            property.Bedrooms,
            property.Bathrooms,
            property.AreaM2,
            property.Amenities,
            property.CreatedAt);
    }
}
