namespace TheHouse.Application.Contracts.Properties;

public sealed record PropertyListResponse(
    IReadOnlyList<PropertyListItemResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);
