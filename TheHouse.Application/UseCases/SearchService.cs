using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Search;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-14: criterios crudos de la query; el servicio los valida.
public sealed record SearchCriteria
{
    public string? UniversityId { get; init; }

    public string? MinPrice { get; init; }

    public string? MaxPrice { get; init; }

    public string? RoomType { get; init; }

    public string? Bathroom { get; init; }

    public string? BuildingType { get; init; }

    public string? AllowedTenants { get; init; }

    public string? Services { get; init; }

    public string? MaxDistanceKm { get; init; }

    public string? Sort { get; init; }

    public string? Bbox { get; init; }
}

// NV-14: ubicación aproximada. El centro de una celda de 0.003° (unos
// 330 m) no revela el punto real y es estable para el mismo edificio.
public static class ApproximateLocation
{
    public const double CellDegrees = 0.003;

    public static (double Latitude, double Longitude) Cell(double latitude, double longitude) =>
        (Center(latitude), Center(longitude));

    private static double Center(double value) =>
        Math.Round((Math.Floor(value / CellDegrees) + 0.5) * CellDegrees, 5);
}

// NV-14: búsqueda pública de cuartos `available` de edificios `verified`.
// Paso 1: edificios cercanos a la universidad; paso 2: sus cuartos.
public sealed class SearchService(
    IUniversityRepository universities,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IPlaceRepository places)
{
    public const double DefaultMaxDistanceKm = 10;

    public const int MaximumMarkers = 300;

    public const double MaximumBboxSide = 0.3;

    public const int MaximumPlaces = 10;

    public async Task<RoomSearchResponse> SearchAsync(SearchCriteria criteria, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var sort = criteria.Sort ?? "distance";
        if (sort is not ("distance" or "price" or "newest"))
        {
            throw new RequestValidationException("sort", "sort must be distance, price or newest.");
        }

        var candidates = await CandidatesAsync(criteria, cancellationToken);
        IEnumerable<Candidate> ordered = sort switch
        {
            "price" => candidates.OrderBy(item => item.Room.Price).ThenBy(item => item.DistanceKm).ThenBy(item => item.Room.Id, StringComparer.Ordinal),
            "newest" => candidates.OrderByDescending(item => item.Room.CreatedAt).ThenBy(item => item.Room.Id, StringComparer.Ordinal),
            _ => candidates.OrderBy(item => item.DistanceKm).ThenBy(item => item.Room.Price).ThenBy(item => item.Room.Id, StringComparer.Ordinal),
        };

        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(ToItem).ToArray();
        return new RoomSearchResponse(items, page, pageSize, candidates.Count, PropertyQueryValidation.CalculateTotalPages(candidates.Count, pageSize));
    }

    public async Task<RoomMapResponse> MapAsync(SearchCriteria criteria, CancellationToken cancellationToken)
    {
        var (minLng, minLat, maxLng, maxLat) = Bbox(criteria.Bbox);
        var candidates = await CandidatesAsync(criteria, cancellationToken);
        var markers = candidates
            .Where(item => item.CellLatitude >= minLat && item.CellLatitude <= maxLat && item.CellLongitude >= minLng && item.CellLongitude <= maxLng)
            .OrderBy(item => item.DistanceKm)
            .ThenBy(item => item.Room.Id, StringComparer.Ordinal)
            .Take(MaximumMarkers)
            .Select(item => new RoomMarker(item.Room.Id, item.Room.Price ?? 0, new ApproxLocation(item.CellLatitude, item.CellLongitude), Math.Round(item.DistanceKm, 1)))
            .ToArray();
        return new RoomMapResponse(markers);
    }

    public async Task<RoomCountResponse> CountAsync(SearchCriteria criteria, CancellationToken cancellationToken) =>
        new((await CandidatesAsync(criteria, cancellationToken)).Count);

    public async Task<RoomDetailResponse> DetailAsync(string? roomId, CancellationToken cancellationToken)
    {
        var id = ModerationRules.ObjectId("id", roomId);
        var room = await rooms.GetAsync(id, cancellationToken);
        if (room is null || room.Status != RoomStatus.Available)
        {
            throw new NotFoundException("room");
        }

        var building = await buildings.GetAsync(room.BuildingId, cancellationToken);
        if (building is null || building.Status != BuildingStatus.Verified)
        {
            throw new NotFoundException("room");
        }

        var university = await universities.GetByIdAsync(building.NearestUniversityId, cancellationToken);
        var (cellLat, cellLng) = ApproximateLocation.Cell(building.Latitude, building.Longitude);
        var distance = university?.Latitude is { } uLat && university.Longitude is { } uLng
            ? ListingRules.DistanceKm(cellLat, cellLng, uLat, uLng)
            : 0;

        return new RoomDetailResponse(
            room.Id,
            building.Id,
            Title(building, room),
            room.Price ?? 0,
            "MXN",
            room.Type,
            room.Bathroom,
            room.SizeM2,
            room.Furniture,
            room.AvailableFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            room.MinContract,
            Math.Round(distance, 1),
            Walking(distance),
            new ApproxLocation(cellLat, cellLng),
            university is null ? null : new SearchUniversity(university.Id, university.Code, university.Name),
            new SearchBuilding(building.Id, building.Name, building.Type, building.AllowedTenants, building.Services, building.Rules),
            null,
            building.Demo);
    }

    public async Task<PlaceSearchResponse> PlacesAsync(string? query, string? universityId, CancellationToken cancellationToken)
    {
        var university = await UniversityAsync(universityId, cancellationToken);
        var trimmed = query?.Trim();
        if (trimmed is null || trimmed.Length is < 2 or > 60)
        {
            throw new RequestValidationException("q", "q must have 2 to 60 characters.");
        }

        var normalized = SearchText.Normalize(trimmed);
        if (normalized.Length == 0)
        {
            throw new RequestValidationException("q", "q must contain letters or digits.");
        }

        var found = await places.SearchAsync(normalized, 50, cancellationToken);
        var items = found
            .Select(place => (Place: place, Distance: ListingRules.DistanceKm(place.Latitude, place.Longitude, university.Latitude!.Value, university.Longitude!.Value)))
            .OrderBy(item => SearchText.Normalize(item.Place.Name).StartsWith(normalized, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(item => item.Distance)
            .Take(MaximumPlaces)
            .Select(item => new PlaceItem(item.Place.Id, item.Place.Name, item.Place.Type, new ApproxLocation(item.Place.Latitude, item.Place.Longitude), Math.Round(item.Distance, 1)))
            .ToArray();
        return new PlaceSearchResponse(items);
    }

    private async Task<List<Candidate>> CandidatesAsync(SearchCriteria criteria, CancellationToken cancellationToken)
    {
        var university = await UniversityAsync(criteria.UniversityId, cancellationToken);
        var minPrice = Price("minPrice", criteria.MinPrice);
        var maxPrice = Price("maxPrice", criteria.MaxPrice);
        if (minPrice is { } min && maxPrice is { } max && min > max)
        {
            throw new RequestValidationException("minPrice", "minPrice must not exceed maxPrice.");
        }

        var roomType = Optional("roomType", criteria.RoomType, ListingRules.RoomTypes);
        var bathroom = Optional("bathroom", criteria.Bathroom, ListingRules.Bathrooms);
        var buildingType = Optional("buildingType", criteria.BuildingType, ListingRules.BuildingTypes);
        var tenants = Optional("allowedTenants", criteria.AllowedTenants, ListingRules.AllowedTenants);
        var services = criteria.Services is null
            ? Array.Empty<string>()
            : ListingRules.Distinct("services", criteria.Services.Split(',', StringSplitOptions.TrimEntries), ListingRules.Services);
        var maxDistance = Distance(criteria.MaxDistanceKm);

        var (uniLat, uniLng) = (university.Latitude!.Value, university.Longitude!.Value);

        // Margen de media celda: la distancia publicada se mide desde el centro de la celda.
        var near = await buildings.ListVerifiedNearAsync(uniLat, uniLng, maxDistance + 0.25, cancellationToken);
        var selected = new Dictionary<string, (Building Building, double Lat, double Lng, double Distance)>(StringComparer.Ordinal);
        foreach (var building in near)
        {
            if (building.Status != BuildingStatus.Verified ||
                (buildingType is not null && building.Type != buildingType) ||
                (tenants is not null && building.AllowedTenants != tenants && !(tenants != "any" && building.AllowedTenants == "any")) ||
                services.Any(service => !building.Services.Contains(service, StringComparer.Ordinal)))
            {
                continue;
            }

            var (cellLat, cellLng) = ApproximateLocation.Cell(building.Latitude, building.Longitude);
            var distance = ListingRules.DistanceKm(cellLat, cellLng, uniLat, uniLng);
            if (distance <= maxDistance)
            {
                selected[building.Id] = (building, cellLat, cellLng, distance);
            }
        }

        if (selected.Count == 0)
        {
            return [];
        }

        var available = await rooms.ListAvailableByBuildingsAsync(selected.Keys.ToArray(), new RoomFilter(minPrice, maxPrice, roomType, bathroom), cancellationToken);
        return available
            .Where(room => room.Status == RoomStatus.Available && room.Price is not null && selected.ContainsKey(room.BuildingId))
            .Select(room =>
            {
                var (building, lat, lng, distance) = selected[room.BuildingId];
                return new Candidate(room, building, lat, lng, distance);
            })
            .ToList();
    }

    private async Task<University> UniversityAsync(string? id, CancellationToken cancellationToken)
    {
        if (!UniversityQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException("universityId", "universityId must be a 24-character hexadecimal ObjectId.");
        }

        var university = await universities.GetByIdAsync(id!.ToLowerInvariant(), cancellationToken);
        return university is { Latitude: not null, Longitude: not null }
            ? university
            : throw new NotFoundException("university");
    }

    private static RoomSearchItem ToItem(Candidate item) =>
        new(
            item.Room.Id,
            item.Building.Id,
            Title(item.Building, item.Room),
            item.Room.Price ?? 0,
            "MXN",
            item.Room.Type,
            item.Room.Bathroom,
            item.Building.Type,
            item.Building.AllowedTenants,
            item.Building.Services,
            Math.Round(item.DistanceKm, 1),
            Walking(item.DistanceKm),
            new ApproxLocation(item.CellLatitude, item.CellLongitude),
            null,
            item.Building.Demo);

    private static string Title(Building building, Room room) => $"{building.Name} · Cuarto {room.Number}";

    private static int Walking(double distanceKm) => (int)Math.Ceiling(distanceKm / ListingRules.WalkingSpeedKmPerHour * 60);

    private static int? Price(string field, string? value)
    {
        if (value is null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var price) &&
               price is >= ListingRules.MinimumPrice and <= ListingRules.MaximumPrice
            ? price
            : throw new RequestValidationException(field, $"{field} must be 500 to 50000.");
    }

    private static double Distance(string? value)
    {
        if (value is null)
        {
            return DefaultMaxDistanceKm;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) &&
               double.IsFinite(distance) && distance is >= 0.5 and <= 30
            ? distance
            : throw new RequestValidationException("maxDistanceKm", "maxDistanceKm must be 0.5 to 30.");
    }

    private static string? Optional(string field, string? value, IReadOnlySet<string> catalog) =>
        value is null ? null : ListingRules.OneOf(field, value, catalog);

    private static (double MinLng, double MinLat, double MaxLng, double MaxLat) Bbox(string? value)
    {
        var parts = value?.Split(',');
        if (parts is not { Length: 4 } ||
            !parts.All(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)))
        {
            throw new RequestValidationException("bbox", "bbox must be minLng,minLat,maxLng,maxLat.");
        }

        var numbers = parts.Select(part => double.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        var (minLng, minLat, maxLng, maxLat) = (numbers[0], numbers[1], numbers[2], numbers[3]);
        if (minLng >= maxLng || minLat >= maxLat ||
            maxLng - minLng > MaximumBboxSide || maxLat - minLat > MaximumBboxSide ||
            minLat < -90 || maxLat > 90 || minLng < -180 || maxLng > 180)
        {
            throw new RequestValidationException("bbox", "bbox must be valid and at most 0.3 degrees per side.");
        }

        return (minLng, minLat, maxLng, maxLat);
    }

    private sealed record Candidate(Room Room, Building Building, double CellLatitude, double CellLongitude, double DistanceKm);
}

// NV-14: seeds autorizados de colonias y de demostración.
public sealed class PlaceSeedOptions
{
    public bool Enabled { get; init; }
}

public sealed class DemoRoomSeedOptions
{
    public bool Enabled { get; init; }
}

public sealed class PlaceSeedService(IPlaceRepository places)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var data = PlaceSeedData.Create();
        await places.UpsertSeedAsync(data, cancellationToken);
        return data.Count;
    }
}

// Convierte las 12 propiedades de NV-07 en cuartos verificados `demo` de un
// arrendador de sistema que no tiene cuenta ni puede iniciar sesión. Es
// idempotente: el edificio guarda la propiedad de origen (`DemoSourceId`).
public sealed class DemoRoomSeedService(
    IUniversityRepository universities,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    ILandlordProfileRepository landlords,
    TimeProvider timeProvider)
{
    public const string SystemLandlordId = "system-demo";

    private static readonly Dictionary<string, string[]> AmenityServices = new(StringComparer.Ordinal)
    {
        ["wifi"] = ["wifi"],
        ["amueblado"] = ["furnished"],
        ["cocina equipada"] = ["kitchen"],
        ["estacionamiento"] = ["parking"],
        ["lavadora"] = ["laundry"],
        ["servicios incluidos"] = ["water", "electricity"],
    };

    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var (catalog, _) = await universities.ListAsync(1, 50, cancellationToken);
        var byCode = catalog.ToDictionary(university => university.Code, StringComparer.Ordinal);
        var existing = (await buildings.ListDemoAsync(cancellationToken)).Select(building => building.DemoSourceId).ToHashSet(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();

        await landlords.CreateAsync(
            new LandlordProfile
            {
                UserId = SystemLandlordId,
                FullName = "TheHouse (ejemplos)",
                Phone = "+520000000000",
                RentalMode = "agency",
                AcceptedTermsVersion = "system",
                AcceptedTermsAt = now,
                CreatedAt = now,
            },
            cancellationToken);

        var created = 0;
        foreach (var record in PropertySeedData.Create().Where(record => !existing.Contains(record.Id)))
        {
            var university = byCode.TryGetValue(record.UniversityCode, out var found) && found.Latitude is not null
                ? found
                : throw new InvalidOperationException($"Demo seed requires university '{record.UniversityCode}' with coordinates. Run the university seed first.");

            var (latitude, longitude) = Offset(record.Id, university.Latitude!.Value, university.Longitude!.Value);
            var (distance, minutes) = ListingRules.Walking(latitude, longitude, university.Latitude.Value, university.Longitude!.Value);
            var services = record.Amenities.SelectMany(amenity => AmenityServices.GetValueOrDefault(amenity) ?? []).Distinct(StringComparer.Ordinal).ToArray();
            var privateBath = record.Amenities.Contains("baño privado", StringComparer.Ordinal);

            var building = await buildings.CreateAsync(
                new Building
                {
                    OwnerId = SystemLandlordId,
                    Type = record.Bedrooms >= 2 ? "full_apartment" : "rooms_building",
                    Name = record.Title,
                    Address = new BuildingAddress("Ubicación de ejemplo", "s/n", "Centro", record.City, State(record.City), "00000"),
                    Latitude = latitude,
                    Longitude = longitude,
                    NearestUniversityId = university.Id,
                    DistanceToUniversityKm = distance,
                    WalkingMinutes = minutes,
                    AllowedTenants = "any",
                    Services = services,
                    Rules = "Publicación de ejemplo de TheHouse.",
                    Status = BuildingStatus.Verified,
                    Version = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Demo = true,
                    DemoSourceId = record.Id,
                },
                cancellationToken);

            await rooms.CreateManyAsync(
                [
                    new Room
                    {
                        BuildingId = building.Id,
                        OwnerId = SystemLandlordId,
                        Number = 101,
                        Floor = 1,
                        Price = (int)Math.Clamp(Math.Round(record.Price), ListingRules.MinimumPrice, ListingRules.MaximumPrice),
                        SizeM2 = Math.Clamp(Math.Round((decimal)record.AreaM2, 1), ListingRules.MinimumSizeM2, ListingRules.MaximumSizeM2),
                        Type = record.Bedrooms >= 2 ? "double" : "single",
                        Bathroom = privateBath || record.Bedrooms >= 2 ? "private" : "shared",
                        Furniture = record.Amenities.Contains("amueblado", StringComparer.Ordinal) ? ["bed", "desk", "closet"] : [],
                        AvailableFrom = new DateOnly(2026, 12, 1),
                        MinContract = "six_months",
                        Status = RoomStatus.Available,
                        Version = 1,
                        CreatedAt = now,
                        UpdatedAt = now,
                    },
                ],
                cancellationToken);
            created++;
        }

        return created;
    }

    private static string State(string city) =>
        city is "Jilotepec" or "Villa del Carbón" ? "Estado de México" : "Hidalgo";

    // Punto estable entre 0.5 y 2 km de la universidad, derivado del id.
    private static (double Latitude, double Longitude) Offset(string seed, double latitude, double longitude)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        var bearing = (((hash[0] << 8) | hash[1]) % 360) * Math.PI / 180;
        var distanceKm = 0.5 + ((((hash[2] << 8) | hash[3]) % 1500) / 1000.0);
        var dLat = distanceKm / 111.32 * Math.Cos(bearing);
        var dLng = distanceKm / (111.32 * Math.Cos(latitude * Math.PI / 180)) * Math.Sin(bearing);
        return (Math.Round(latitude + dLat, 6), Math.Round(longitude + dLng, 6));
    }
}
