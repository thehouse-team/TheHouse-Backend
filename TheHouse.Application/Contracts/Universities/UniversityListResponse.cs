namespace TheHouse.Application.Contracts.Universities;

public sealed record UniversityListResponse(
    IReadOnlyList<UniversityResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);
