namespace TheHouse.Application.Pagination;

public sealed class PageResult<T>
{
    private PageResult(IReadOnlyList<T> items, PageRequest request, long totalItems)
    {
        Items = items.ToArray();
        Page = request.Page;
        PageSize = request.PageSize;
        TotalItems = totalItems;
        TotalPages = totalItems / PageSize + (totalItems % PageSize == 0 ? 0 : 1);
    }

    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public long TotalItems { get; }
    public long TotalPages { get; }

    public static PageResult<T> Create(IReadOnlyList<T> items, PageRequest request, long totalItems)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (totalItems < 0 || items.Count > totalItems || items.Count > request.PageSize)
            throw new ArgumentOutOfRangeException(nameof(totalItems), "Metadatos de paginación inconsistentes.");
        return new PageResult<T>(items, request, totalItems);
    }
}
