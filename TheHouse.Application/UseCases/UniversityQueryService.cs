using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Universities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

public sealed class UniversityQueryService(
    IUniversityRepository universityRepository)
{
    public async Task<UniversityListResponse> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        UniversityQueryValidation.EnsureValidPagination(
            page,
            pageSize);

        var (items, totalItems) =
            await universityRepository.ListAsync(
                page,
                pageSize,
                cancellationToken);

        var mapped = items
            .Select(static university =>
                new UniversityResponse(
                    university.Id,
                    university.Code,
                    university.Name,
                    university.City,
                    university.Country,
                    university.CreatedAt))
            .ToArray();

        return new UniversityListResponse(
            mapped,
            page,
            pageSize,
            totalItems,
            UniversityQueryValidation.CalculateTotalPages(
                totalItems,
                pageSize));
    }

    public async Task<UniversityResponse> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!UniversityQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException(
                "id",
                "id must be a 24-character hexadecimal ObjectId.");
        }

        var university =
            await universityRepository.GetByIdAsync(
                id,
                cancellationToken)
            ?? throw new NotFoundException(
                "University");

        return new UniversityResponse(
            university.Id,
            university.Code,
            university.Name,
            university.City,
            university.Country,
            university.CreatedAt);
    }
}
