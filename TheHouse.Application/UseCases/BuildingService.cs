using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-12: edificios del arrendador. Solo el dueño los ve (ajeno → 404);
// escrituras con concurrencia optimista (`version`).
public sealed class BuildingService(
    LandlordService landlords,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IUniversityRepository universities,
    TimeProvider timeProvider)
{
    public async Task<BuildingResponse> CreateAsync(string userId, BuildingRequest request, CancellationToken cancellationToken)
    {
        await landlords.RequireAsync(userId, cancellationToken);
        if (request.Version is not null)
        {
            throw new RequestValidationException("version", "version is not accepted on create.");
        }

        if (await buildings.CountByOwnerAsync(userId, cancellationToken) >= ListingRules.MaximumBuildingsPerLandlord)
        {
            throw new ConflictException("buildings_limit");
        }

        var (latitude, longitude) = Location(request.Location);
        var university = await UniversityAsync(request.NearestUniversityId, cancellationToken);
        var (distance, minutes) = ListingRules.Walking(latitude, longitude, university.Latitude!.Value, university.Longitude!.Value);
        var now = timeProvider.GetUtcNow();

        var created = await buildings.CreateAsync(
            new Building
            {
                OwnerId = userId,
                Type = ListingRules.OneOf("type", request.Type, ListingRules.BuildingTypes),
                Name = ListingRules.Text("name", request.Name, 2, 120),
                Address = Address(request.Address),
                Latitude = latitude,
                Longitude = longitude,
                NearestUniversityId = university.Id,
                DistanceToUniversityKm = distance,
                WalkingMinutes = minutes,
                AllowedTenants = ListingRules.OneOf("allowedTenants", request.AllowedTenants, ListingRules.AllowedTenants),
                Services = ListingRules.Distinct("services", request.Services, ListingRules.Services),
                Rules = Rules(request.Rules),
                Status = BuildingStatus.Draft,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
            },
            cancellationToken);

        return ToResponse(created, university, Array.Empty<Room>());
    }

    public async Task<BuildingListResponse> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await landlords.RequireAsync(userId, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);

        var (items, total) = await buildings.ListByOwnerAsync(userId, page, pageSize, cancellationToken);
        var allBuildings = await buildings.ListAllByOwnerAsync(userId, cancellationToken);
        var allRooms = await rooms.ListAllByOwnerAsync(userId, cancellationToken);
        var byBuilding = allRooms.ToLookup(room => room.BuildingId, StringComparer.Ordinal);
        var universityById = await UniversitiesAsync(items.Select(building => building.NearestUniversityId), cancellationToken);

        var rented = allRooms.Where(room => room.Status == RoomStatus.Rented).ToArray();
        var summary = new LandlordSummary(
            allBuildings.Count,
            allRooms.Count,
            allRooms.Count(room => room.Status == RoomStatus.Available),
            allRooms.Count == 0 ? 0 : (int)Math.Round(rented.Length * 100.0 / allRooms.Count),
            rented.Sum(room => (long)(room.Price ?? 0)));

        return new BuildingListResponse(
            items.Select(building => ToResponse(building, universityById.GetValueOrDefault(building.NearestUniversityId), byBuilding[building.Id].ToArray())).ToArray(),
            page,
            pageSize,
            total,
            PropertyQueryValidation.CalculateTotalPages(total, pageSize),
            summary);
    }

    public async Task<BuildingResponse> GetAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var building = await LoadOwnedAsync(userId, id, cancellationToken);
        return await ResponseAsync(building, cancellationToken);
    }

    public async Task<BuildingResponse> UpdateAsync(string userId, string? id, BuildingRequest request, CancellationToken cancellationToken)
    {
        var current = await LoadOwnedAsync(userId, id, cancellationToken);
        var expected = request.Version ?? throw new RequestValidationException("version", "version is required.");
        if (expected != current.Version)
        {
            throw new ConflictException("version_conflict");
        }

        if (!current.AcceptsChanges)
        {
            throw new ConflictException("building_in_review");
        }

        var updated = current;
        if (request.Type is not null)
        {
            updated = updated with { Type = ListingRules.OneOf("type", request.Type, ListingRules.BuildingTypes) };
        }

        if (request.Name is not null)
        {
            updated = updated with { Name = ListingRules.Text("name", request.Name, 2, 120) };
        }

        if (request.Address is not null)
        {
            updated = updated with { Address = Address(request.Address) };
        }

        if (request.AllowedTenants is not null)
        {
            updated = updated with { AllowedTenants = ListingRules.OneOf("allowedTenants", request.AllowedTenants, ListingRules.AllowedTenants) };
        }

        if (request.Services is not null)
        {
            updated = updated with { Services = ListingRules.Distinct("services", request.Services, ListingRules.Services) };
        }

        if (request.Rules is not null)
        {
            updated = updated with { Rules = Rules(request.Rules) };
        }

        if (request.Location is not null)
        {
            var (latitude, longitude) = Location(request.Location);
            updated = updated with { Latitude = latitude, Longitude = longitude };
        }

        if (request.NearestUniversityId is not null)
        {
            updated = updated with { NearestUniversityId = (await UniversityAsync(request.NearestUniversityId, cancellationToken)).Id };
        }

        var university = await UniversityAsync(updated.NearestUniversityId, cancellationToken);
        var (distance, minutes) = ListingRules.Walking(updated.Latitude, updated.Longitude, university.Latitude!.Value, university.Longitude!.Value);
        updated = updated with { DistanceToUniversityKm = distance, WalkingMinutes = minutes };

        // Cambios sensibles en un edificio verificado lo regresan a revisión.
        var sensitive = updated.Type != current.Type ||
                        updated.Address != current.Address ||
                        updated.Latitude != current.Latitude ||
                        updated.Longitude != current.Longitude;
        if (current.Status == BuildingStatus.Verified && sensitive)
        {
            updated = updated with { Status = BuildingStatus.InReview };
        }

        updated = updated with { Version = expected + 1, UpdatedAt = timeProvider.GetUtcNow() };
        await SaveAsync(updated, expected, cancellationToken);
        return await ResponseAsync(updated, cancellationToken);
    }

    public async Task DeleteAsync(string userId, string? id, string? version, CancellationToken cancellationToken)
    {
        var building = await LoadOwnedAsync(userId, id, cancellationToken);
        var expected = ParseVersion(version);
        if (expected != building.Version)
        {
            throw new ConflictException("version_conflict");
        }

        if (!building.AcceptsChanges)
        {
            throw new ConflictException("building_in_review");
        }

        var buildingRooms = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        if (buildingRooms.Any(room => room.Status == RoomStatus.Rented))
        {
            throw new ConflictException("building_has_rented_rooms");
        }

        if (!await buildings.DeleteAsync(building.Id, expected, cancellationToken))
        {
            throw new ConflictException("version_conflict");
        }

        await rooms.DeleteByBuildingAsync(building.Id, cancellationToken);
    }

    public async Task<PublicationResponse> PublicationAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var building = await LoadOwnedAsync(userId, id, cancellationToken);
        return await StepsAsync(building, cancellationToken);
    }

    public async Task<BuildingResponse> PublishAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var building = await LoadOwnedAsync(userId, id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var submitted = building.SubmitForReview(now);

        var publication = await StepsAsync(building, cancellationToken);
        if (!publication.CanPublish)
        {
            var missing = string.Join(", ", publication.Steps.Where(step => !step.Done).Select(step => step.Id));
            throw new ConflictException("publication_incomplete", $"Faltan pasos para enviar a revisión: {missing}.");
        }

        submitted = submitted with { Version = building.Version + 1 };
        await SaveAsync(submitted, building.Version, cancellationToken);
        await rooms.SetStatusByBuildingAsync(building.Id, RoomStatus.Ready, RoomStatus.InReview, now, cancellationToken);
        return await ResponseAsync(submitted, cancellationToken);
    }

    public async Task<Building> LoadOwnedAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        await landlords.RequireAsync(userId, cancellationToken);
        if (!PropertyQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException("id", "id must be a 24-character hexadecimal ObjectId.");
        }

        var building = await buildings.GetAsync(id!.ToLowerInvariant(), cancellationToken);
        return building is not null && building.OwnerId == userId
            ? building
            : throw new NotFoundException("building");
    }

    public static long ParseVersion(string? version) =>
        long.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new RequestValidationException("version", "version is required.");

    private async Task<PublicationResponse> StepsAsync(Building building, CancellationToken cancellationToken)
    {
        var buildingRooms = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        var steps = new[]
        {
            new PublicationStep("building_data", building.Name.Length > 0 && building.Address.Street.Length > 0),
            new PublicationStep("location", building.NearestUniversityId.Length > 0),
            new PublicationStep("services", building.Services.Count > 0),
            new PublicationStep("room_ready", buildingRooms.Any(room => room.Status == RoomStatus.Ready)),
        };

        return new PublicationResponse(building.Status == BuildingStatus.Draft && steps.All(step => step.Done), steps);
    }

    private async Task SaveAsync(Building building, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!await buildings.ReplaceAsync(building, expectedVersion, cancellationToken))
        {
            throw await buildings.GetAsync(building.Id, cancellationToken) is null
                ? new NotFoundException("building")
                : new ConflictException("version_conflict");
        }
    }

    private async Task<BuildingResponse> ResponseAsync(Building building, CancellationToken cancellationToken)
    {
        var university = await universities.GetByIdAsync(building.NearestUniversityId, cancellationToken);
        var buildingRooms = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        return ToResponse(building, university, buildingRooms);
    }

    private async Task<University> UniversityAsync(string? id, CancellationToken cancellationToken)
    {
        if (!UniversityQueryValidation.IsObjectId(id))
        {
            throw new RequestValidationException("nearestUniversityId", "nearestUniversityId must be a 24-character hexadecimal ObjectId.");
        }

        var university = await universities.GetByIdAsync(id!.ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException("university");

        return university.Latitude is not null && university.Longitude is not null
            ? university
            : throw new RequestValidationException("nearestUniversityId", "The university has no location yet.");
    }

    private async Task<Dictionary<string, University>> UniversitiesAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, University>(StringComparer.Ordinal);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (await universities.GetByIdAsync(id, cancellationToken) is { } university)
            {
                result[id] = university;
            }
        }

        return result;
    }

    private static (double Latitude, double Longitude) Location(LocationRequest? location)
    {
        // México continental e islas: evita coordenadas invertidas o vacías.
        if (location?.Lat is not { } lat || location.Lng is not { } lng ||
            !double.IsFinite(lat) || !double.IsFinite(lng) ||
            lat is < 14 or > 33 || lng is < -118.5 or > -86)
        {
            throw new RequestValidationException("location", "location must be a point in Mexico.");
        }

        return (lat, lng);
    }

    private static BuildingAddress Address(AddressRequest? address)
    {
        if (address is null)
        {
            throw new RequestValidationException("address", "address is required.");
        }

        var postalCode = address.PostalCode?.Trim();
        if (postalCode is null || postalCode.Length != 5 || !postalCode.All(char.IsAsciiDigit))
        {
            throw new RequestValidationException("address.postalCode", "postalCode must have 5 digits.");
        }

        return new BuildingAddress(
            ListingRules.Text("address.street", address.Street, 1, 120),
            ListingRules.Text("address.number", address.Number, 1, 20),
            ListingRules.Text("address.neighborhood", address.Neighborhood, 1, 120),
            ListingRules.Text("address.city", address.City, 1, 80),
            ListingRules.Text("address.state", address.State, 1, 60),
            postalCode);
    }

    private static string? Rules(string? rules)
    {
        if (rules is null)
        {
            return null;
        }

        var trimmed = rules.Trim();
        return trimmed.Length <= ListingRules.MaximumRulesLength
            ? trimmed
            : throw new RequestValidationException("rules", "rules is too long.");
    }

    public static BuildingResponse ToResponse(Building building, University? university, IReadOnlyCollection<Room> buildingRooms) =>
        new(
            building.Id,
            building.Type,
            building.Name,
            new AddressResponse(building.Address.Street, building.Address.Number, building.Address.Neighborhood, building.Address.City, building.Address.State, building.Address.PostalCode),
            new LocationResponse(building.Latitude, building.Longitude),
            university is null ? null : new UniversityReference(university.Id, university.Code, university.Name),
            building.DistanceToUniversityKm,
            building.WalkingMinutes,
            building.AllowedTenants,
            building.Services,
            building.Rules,
            ListingRules.BuildingStatusText(building.Status),
            buildingRooms.Count,
            buildingRooms.Count(room => room.Status == RoomStatus.Available),
            building.Version,
            building.CreatedAt,
            building.UpdatedAt);
}
