using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Domain.Entities;

namespace TheHouse.Application.UseCases;

// NV-11 / RF-07: búsqueda pública de universidades por texto (siglas,
// nombre o ciudad, parcial y sin acentos) o por cercanía. Las coordenadas
// del usuario solo se usan para calcular distancias: no se guardan ni se
// registran.
public sealed class UniversitySearchService(IUniversityRepository universities)
{
    public const int MaximumResults = 20;

    public const int MinimumQueryLength = 2;

    public const int MaximumQueryLength = 60;

    private const double EarthRadiusKm = 6371.0088;

    public async Task<UniversitySearchResponse> SearchAsync(
        string? query,
        string? latitude,
        string? longitude,
        CancellationToken cancellationToken)
    {
        var hasQuery = query is not null;
        var hasCoordinates = latitude is not null || longitude is not null;

        if (hasQuery == hasCoordinates)
        {
            throw new RequestValidationException("q", "Send either q or lat and lng.");
        }

        if (hasQuery)
        {
            var trimmed = query!.Trim();
            if (trimmed.Length is < MinimumQueryLength or > MaximumQueryLength)
            {
                throw new RequestValidationException("q", "q must have 2 to 60 characters.");
            }

            var normalized = SearchText.Normalize(trimmed);
            if (normalized.Length == 0)
            {
                throw new RequestValidationException("q", "q must contain letters or digits.");
            }

            var found = await universities.SearchByTextAsync(normalized, MaximumResults, cancellationToken);
            return new UniversitySearchResponse(found.Select(university => ToItem(university, null)).ToArray());
        }

        var lat = ParseCoordinate("lat", latitude, 90);
        var lng = ParseCoordinate("lng", longitude, 180);

        var near = await universities.SearchNearAsync(lat, lng, MaximumResults, cancellationToken);
        var items = near
            .Where(university => university.Latitude is not null && university.Longitude is not null)
            .Select(university => ToItem(
                university,
                Math.Round(DistanceKm(lat, lng, university.Latitude!.Value, university.Longitude!.Value), 1)))
            .OrderBy(item => item.DistanceKm)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

        return new UniversitySearchResponse(items);
    }

    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var dLat = Radians(lat2 - lat1);
        var dLng = Radians(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ParseCoordinate(string name, string? value, double limit)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed) ||
            Math.Abs(parsed) > limit)
        {
            throw new RequestValidationException(name, $"{name} must be a number between -{limit} and {limit}.");
        }

        return parsed;
    }

    private static UniversitySearchItem ToItem(University university, double? distanceKm) =>
        new(university.Id, university.Code, university.Name, university.City, distanceKm);
}
