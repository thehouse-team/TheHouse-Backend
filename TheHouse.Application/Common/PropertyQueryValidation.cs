using System.Globalization;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.Common;

public static class PropertyQueryValidation
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    public static void EnsureValidPagination(
        int page,
        int pageSize)
    {
        if (page < 1)
        {
            throw new RequestValidationException(
                "page",
                "page must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > MaximumPageSize)
        {
            throw new RequestValidationException(
                "pageSize",
                $"pageSize must be between 1 and {MaximumPageSize.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    public static bool IsObjectId(string? value)
    {
        if (value is null || value.Length != 24)
        {
            return false;
        }

        foreach (var character in value)
        {
            var valid =
                character is >= '0' and <= '9' ||
                character is >= 'a' and <= 'f' ||
                character is >= 'A' and <= 'F';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    public static int CalculateTotalPages(
        long totalItems,
        int pageSize)
    {
        if (totalItems <= 0)
        {
            return 0;
        }

        return checked(
            (int)((totalItems + pageSize - 1) / pageSize));
    }
}
