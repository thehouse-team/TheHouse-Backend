using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-12: cuartos de los edificios del arrendador. Número = piso × 100 +
// posición (101, 102, 201…); único por edificio.
public sealed class RoomService(
    BuildingService buildingsService,
    LandlordService landlords,
    IRoomRepository rooms,
    TimeProvider timeProvider)
{
    public async Task<GeneratedRoomsResponse> GenerateAsync(string userId, string? buildingId, GenerateRoomsRequest request, CancellationToken cancellationToken)
    {
        var building = await buildingsService.LoadOwnedAsync(userId, buildingId, cancellationToken);
        building.EnsureEditable();

        if (request.Floors is not { } floors || floors is < 1 or > ListingRules.MaximumFloors)
        {
            throw new RequestValidationException("floors", "floors must be 1 to 30.");
        }

        if (request.RoomsPerFloor is not { } perFloor || perFloor.Count != floors ||
            perFloor.Any(count => count is < 1 or > ListingRules.MaximumRoomsPerFloor))
        {
            throw new RequestValidationException("roomsPerFloor", "roomsPerFloor needs 1 to 30 rooms for each floor.");
        }

        if (request.Numbering is not null and not "floor_prefix")
        {
            throw new RequestValidationException("numbering", "numbering must be floor_prefix.");
        }

        var data = Data(request.Base ?? new RoomDataRequest());
        var existing = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        if (existing.Count + perFloor.Sum() > ListingRules.MaximumRoomsPerBuilding)
        {
            throw new ConflictException("rooms_limit");
        }

        var now = timeProvider.GetUtcNow();
        var created = new List<Room>();
        for (var floor = 1; floor <= floors; floor++)
        {
            if (existing.Count(room => room.Floor == floor) + perFloor[floor - 1] > ListingRules.MaximumRoomsPerFloor)
            {
                throw new ConflictException("rooms_limit");
            }

            for (var position = 1; position <= perFloor[floor - 1]; position++)
            {
                var number = (floor * 100) + position;
                if (existing.Any(room => room.Number == number))
                {
                    throw new ConflictException("room_number_taken");
                }

                created.Add((data with
                {
                    BuildingId = building.Id,
                    OwnerId = userId,
                    Number = number,
                    Floor = floor,
                    Status = RoomStatus.Setup,
                    Version = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                }).WithDerivedStatus());
            }
        }

        var stored = await rooms.CreateManyAsync(created, cancellationToken);
        return new GeneratedRoomsResponse(stored.Select(ToResponse).ToArray());
    }

    public async Task<RoomListResponse> ListAsync(string userId, string? buildingId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var building = await buildingsService.LoadOwnedAsync(userId, buildingId, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (items, total) = await rooms.ListByBuildingAsync(building.Id, page, pageSize, cancellationToken);
        return new RoomListResponse(items.Select(ToResponse).ToArray(), page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<RoomResponse> UpdateAsync(string userId, string? roomId, UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        var current = (await LoadOwnedAsync(userId, roomId, cancellationToken)).EnsureEditable();
        var expected = request.Version ?? throw new RequestValidationException("version", "version is required.");
        if (expected != current.Version)
        {
            throw new ConflictException("version_conflict");
        }

        var updated = Apply(current, new RoomDataRequest
        {
            Price = request.Price,
            SizeM2 = request.SizeM2,
            Type = request.Type,
            Bathroom = request.Bathroom,
            Furniture = request.Furniture,
            AvailableFrom = request.AvailableFrom,
            MinContract = request.MinContract,
        });

        if (request.Floor is { } floor)
        {
            updated = updated with { Floor = floor is >= 1 and <= ListingRules.MaximumFloors ? floor : throw new RequestValidationException("floor", "floor must be 1 to 30.") };
        }

        if (request.Number is { } number)
        {
            updated = updated with { Number = number is >= 1 and <= 9999 ? number : throw new RequestValidationException("number", "number must be 1 to 9999.") };
        }

        updated = updated.WithDerivedStatus() with { Version = expected + 1, UpdatedAt = timeProvider.GetUtcNow() };
        await SaveAsync(updated, expected, cancellationToken);
        return ToResponse(updated);
    }

    public async Task DeleteAsync(string userId, string? roomId, string? version, CancellationToken cancellationToken)
    {
        var room = await LoadOwnedAsync(userId, roomId, cancellationToken);
        var expected = BuildingService.ParseVersion(version);
        if (expected != room.Version)
        {
            throw new ConflictException("version_conflict");
        }

        if (room.Status == RoomStatus.Rented)
        {
            throw new ConflictException("room_rented");
        }

        var building = await buildingsService.LoadOwnedAsync(userId, room.BuildingId, cancellationToken);
        building.EnsureEditable();

        if (!await rooms.DeleteAsync(room.Id, expected, cancellationToken))
        {
            throw new ConflictException("version_conflict");
        }
    }

    public async Task<RoomResponse> DuplicateAsync(string userId, string? roomId, CancellationToken cancellationToken)
    {
        var source = await LoadOwnedAsync(userId, roomId, cancellationToken);
        var building = await buildingsService.LoadOwnedAsync(userId, source.BuildingId, cancellationToken);
        building.EnsureEditable();

        var existing = await rooms.ListAllByBuildingAsync(building.Id, cancellationToken);
        var sameFloor = existing.Where(room => room.Floor == source.Floor).ToArray();
        if (existing.Count >= ListingRules.MaximumRoomsPerBuilding || sameFloor.Length >= ListingRules.MaximumRoomsPerFloor)
        {
            throw new ConflictException("rooms_limit");
        }

        var now = timeProvider.GetUtcNow();
        var copy = (source with
        {
            Id = string.Empty,
            Number = sameFloor.Max(room => room.Number) + 1,
            Status = RoomStatus.Setup,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        }).WithDerivedStatus();

        var stored = await rooms.CreateManyAsync([copy], cancellationToken);
        return ToResponse(stored[0]);
    }

    public async Task<RoomResponse> CopyFromAsync(string userId, string? roomId, CopyRoomRequest request, CancellationToken cancellationToken)
    {
        var target = (await LoadOwnedAsync(userId, roomId, cancellationToken)).EnsureEditable();
        var expected = request.Version ?? throw new RequestValidationException("version", "version is required.");
        if (expected != target.Version)
        {
            throw new ConflictException("version_conflict");
        }

        var source = await LoadOwnedAsync(userId, request.SourceRoomId, cancellationToken);
        if (source.BuildingId != target.BuildingId)
        {
            throw new NotFoundException("room");
        }

        var updated = (target with
        {
            Price = source.Price,
            SizeM2 = source.SizeM2,
            Type = source.Type,
            Bathroom = source.Bathroom,
            Furniture = source.Furniture,
            AvailableFrom = source.AvailableFrom,
            MinContract = source.MinContract,
        }).WithDerivedStatus() with { Version = expected + 1, UpdatedAt = timeProvider.GetUtcNow() };

        await SaveAsync(updated, expected, cancellationToken);
        return ToResponse(updated);
    }

    public Task<RoomResponse> PauseAsync(string userId, string? roomId, CancellationToken cancellationToken) =>
        TransitionAsync(userId, roomId, (room, now) => room.Pause(now), cancellationToken);

    public Task<RoomResponse> ResumeAsync(string userId, string? roomId, CancellationToken cancellationToken) =>
        TransitionAsync(userId, roomId, (room, now) => room.Resume(now), cancellationToken);

    private async Task<RoomResponse> TransitionAsync(
        string userId,
        string? roomId,
        Func<Room, DateTimeOffset, Room> transition,
        CancellationToken cancellationToken)
    {
        var room = await LoadOwnedAsync(userId, roomId, cancellationToken);
        var changed = transition(room, timeProvider.GetUtcNow()) with { Version = room.Version + 1 };
        await SaveAsync(changed, room.Version, cancellationToken);
        return ToResponse(changed);
    }

    private async Task<Room> LoadOwnedAsync(string userId, string? roomId, CancellationToken cancellationToken)
    {
        await landlords.RequireAsync(userId, cancellationToken);
        if (!PropertyQueryValidation.IsObjectId(roomId))
        {
            throw new RequestValidationException("id", "id must be a 24-character hexadecimal ObjectId.");
        }

        var room = await rooms.GetAsync(roomId!.ToLowerInvariant(), cancellationToken);
        return room is not null && room.OwnerId == userId ? room : throw new NotFoundException("room");
    }

    private async Task SaveAsync(Room room, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!await rooms.ReplaceAsync(room, expectedVersion, cancellationToken))
        {
            throw await rooms.GetAsync(room.Id, cancellationToken) is null
                ? new NotFoundException("room")
                : new ConflictException("version_conflict");
        }
    }

    private static Room Data(RoomDataRequest request) => Apply(new Room(), request);

    // Solo los campos presentes cambian; v1 no permite vaciar un dato.
    private static Room Apply(Room room, RoomDataRequest request)
    {
        var result = room;
        if (request.Price is { } price)
        {
            result = result with { Price = price is >= ListingRules.MinimumPrice and <= ListingRules.MaximumPrice ? price : throw new RequestValidationException("price", "price must be 500 to 50000.") };
        }

        if (request.SizeM2 is { } size)
        {
            result = result with
            {
                SizeM2 = size >= ListingRules.MinimumSizeM2 && size <= ListingRules.MaximumSizeM2 && decimal.Round(size, 1) == size
                    ? size
                    : throw new RequestValidationException("sizeM2", "sizeM2 must be 4 to 200 with one decimal."),
            };
        }

        if (request.Type is not null)
        {
            result = result with { Type = ListingRules.OneOf("type", request.Type, ListingRules.RoomTypes) };
        }

        if (request.Bathroom is not null)
        {
            result = result with { Bathroom = ListingRules.OneOf("bathroom", request.Bathroom, ListingRules.Bathrooms) };
        }

        if (request.Furniture is not null)
        {
            result = result with { Furniture = ListingRules.Distinct("furniture", request.Furniture, ListingRules.Furniture) };
        }

        if (request.AvailableFrom is not null)
        {
            result = result with { AvailableFrom = ListingRules.Date("availableFrom", request.AvailableFrom) };
        }

        if (request.MinContract is not null)
        {
            result = result with { MinContract = ListingRules.OneOf("minContract", request.MinContract, ListingRules.MinContracts) };
        }

        return result;
    }

    public static RoomResponse ToResponse(Room room) =>
        new(
            room.Id,
            room.BuildingId,
            room.Number,
            room.Floor,
            room.Price,
            "MXN",
            room.SizeM2,
            room.Type,
            room.Bathroom,
            room.Furniture,
            room.AvailableFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            room.MinContract,
            ListingRules.RoomStatusText(room.Status),
            room.Version,
            room.CreatedAt,
            room.UpdatedAt);
}
