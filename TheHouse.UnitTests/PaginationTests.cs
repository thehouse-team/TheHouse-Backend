using System.ComponentModel.DataAnnotations;
using TheHouse.Application.Pagination;

namespace TheHouse.UnitTests;

public sealed class PaginationTests
{
    [Fact]
    public void Defaults_are_bounded()
    {
        var request = new PageRequest();
        request.Validate();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0, request.Offset);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 51)]
    [InlineData(1, int.MaxValue)]
    public void Invalid_pages_and_sizes_are_rejected(int page, int size) =>
        Assert.Throws<ValidationException>(() => new PageRequest { Page = page, PageSize = size }.Validate());

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(40, 2)]
    public void Total_pages_uses_ceiling(long total, long expected) =>
        Assert.Equal(expected, PageResult<int>.Create([], new PageRequest(), total).TotalPages);

    [Fact]
    public void Large_values_do_not_overflow()
    {
        var request = new PageRequest { Page = int.MaxValue, PageSize = 50 };
        request.Validate();
        Assert.Equal(((long)int.MaxValue - 1) * 50, request.Offset);
        var result = PageResult<int>.Create([], request, long.MaxValue);
        Assert.Equal(long.MaxValue / 50 + 1, result.TotalPages);
    }

    [Fact]
    public void Items_and_metadata_are_bounded_and_snapshotted()
    {
        var items = new List<int> { 1, 2 };
        var result = PageResult<int>.Create(items, new PageRequest { Page = 2, PageSize = 2 }, 5);
        items.Add(3);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void Inconsistent_metadata_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PageResult<int>.Create([], new PageRequest(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageResult<int>.Create([1], new PageRequest(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PageResult<int>.Create(new int[51], new PageRequest { PageSize = 50 }, 100));
    }
}
