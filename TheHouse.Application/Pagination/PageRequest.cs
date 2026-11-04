using System.ComponentModel.DataAnnotations;

namespace TheHouse.Application.Pagination;

public sealed class PageRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 20;

    public long Offset => ((long)Page - 1) * PageSize;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
}
