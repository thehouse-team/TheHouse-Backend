using System.Globalization;
using System.Text.RegularExpressions;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.Common;

// NV-12 §6: catálogos cerrados, límites y validaciones de arrendador,
// edificio y cuarto (validados con Mobile, M-BE-064).
public static partial class ListingRules
{
    public const int MaximumBuildingsPerLandlord = 20;

    public const int MaximumRoomsPerBuilding = 200;

    public const int MaximumFloors = 30;

    public const int MaximumRoomsPerFloor = 30;

    public const int MinimumPrice = 500;

    public const int MaximumPrice = 50_000;

    public const decimal MinimumSizeM2 = 4m;

    public const decimal MaximumSizeM2 = 200m;

    public const int MaximumRulesLength = 2000;

    public const double WalkingSpeedKmPerHour = 5;

    public static readonly IReadOnlySet<string> RentalModes = Set("individual", "agency");

    public static readonly IReadOnlySet<string> BuildingTypes = Set("rooms_building", "house_rooms", "full_apartment");

    public static readonly IReadOnlySet<string> AllowedTenants = Set("women", "men", "any");

    public static readonly IReadOnlySet<string> RoomTypes = Set("single", "double", "shared");

    public static readonly IReadOnlySet<string> Bathrooms = Set("private", "shared");

    public static readonly IReadOnlySet<string> Furniture = Set("bed", "desk", "chair", "closet", "dresser", "tv", "fridge", "microwave");

    public static readonly IReadOnlySet<string> MinContracts = Set("four_months", "six_months", "annual");

    // Servicios: el mismo catálogo que las preferencias de NV-11 (Smart Match).
    public static IReadOnlySet<string> Services => PreferenceCatalogs.EssentialServices;

    public static bool IsValidPhone(string? phone) => phone is not null && MexicanPhone().IsMatch(phone);

    // RFC del SAT: persona moral (12) o física (13) con dígito verificador.
    public static bool IsValidRfc(string? rfc, bool company)
    {
        if (rfc is null)
        {
            return false;
        }

        var value = rfc.ToUpperInvariant();
        if (!(company ? CompanyRfc() : PersonRfc()).IsMatch(value))
        {
            return false;
        }

        const string alphabet = "0123456789ABCDEFGHIJKLMN&OPQRSTUVWXYZ Ñ";
        var padded = value.Length == 12 ? " " + value : value;
        var sum = 0;
        for (var index = 0; index < 12; index++)
        {
            var position = alphabet.IndexOf(padded[index], StringComparison.Ordinal);
            if (position < 0)
            {
                return false;
            }

            sum += position * (13 - index);
        }

        var digit = 11 - (sum % 11);
        var expected = digit switch
        {
            11 => '0',
            10 => 'A',
            _ => (char)('0' + digit),
        };

        return padded[12] == expected;
    }

    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var dLat = Radians(lat2 - lat1);
        var dLng = Radians(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * 6371.0088 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static (double DistanceKm, int WalkingMinutes) Walking(double lat1, double lng1, double lat2, double lng2)
    {
        var distance = DistanceKm(lat1, lng1, lat2, lng2);
        return (Math.Round(distance, 1), (int)Math.Ceiling(distance / WalkingSpeedKmPerHour * 60));
    }

    public static string BuildingStatusText(BuildingStatus status) => status switch
    {
        BuildingStatus.Draft => "draft",
        BuildingStatus.InReview => "in_review",
        BuildingStatus.Verified => "verified",
        BuildingStatus.Rejected => "rejected",
        BuildingStatus.Suspended => "suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string RoomStatusText(RoomStatus status) => status switch
    {
        RoomStatus.Setup => "setup",
        RoomStatus.Ready => "ready",
        RoomStatus.InReview => "in_review",
        RoomStatus.Available => "available",
        RoomStatus.Paused => "paused",
        RoomStatus.Rented => "rented",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string Text(string field, string? value, int minimum, int maximum)
    {
        var trimmed = value?.Trim();
        if (trimmed is null || trimmed.Length < minimum || trimmed.Length > maximum || trimmed.Any(char.IsControl))
        {
            throw new RequestValidationException(field, $"{field} must have {minimum} to {maximum} characters.");
        }

        return trimmed;
    }

    public static string OneOf(string field, string? value, IReadOnlySet<string> catalog) =>
        value is not null && catalog.Contains(value)
            ? value
            : throw new RequestValidationException(field, $"{field} has an unknown value.");

    public static IReadOnlyList<string> Distinct(string field, IReadOnlyList<string>? values, IReadOnlySet<string> catalog)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        if (values.Any(value => value is null || !catalog.Contains(value)) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new RequestValidationException(field, $"{field} has unknown or repeated values.");
        }

        return values.ToArray();
    }

    public static DateOnly Date(string field, string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new RequestValidationException(field, $"{field} must be YYYY-MM-DD.");

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    [GeneratedRegex(@"^\+52\d{10}$", RegexOptions.CultureInvariant)]
    private static partial Regex MexicanPhone();

    [GeneratedRegex(@"^[A-ZÑ&]{3}\d{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyRfc();

    [GeneratedRegex(@"^[A-ZÑ&]{4}\d{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PersonRfc();
}
