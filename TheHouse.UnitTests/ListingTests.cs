using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Landlord;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-12: reglas del dominio y de aplicación para arrendador, edificios y cuartos.
public sealed class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("SAT970701NN3", true, true)]
    [InlineData("sat970701nn3", true, true)]
    [InlineData("SAT970701NN4", true, false)]
    [InlineData("SAT97070NN3", true, false)]
    [InlineData("SATX970701NN3", true, false)]
    [InlineData(null, true, false)]
    public void Rfc_validation_checks_format_and_check_digit(string? rfc, bool company, bool expected)
    {
        Assert.Equal(expected, ListingRules.IsValidRfc(rfc, company));
    }

    [Fact]
    public void Person_rfc_uses_the_same_check_digit_algorithm()
    {
        // Se construye un RFC de persona física con su dígito correcto y se
        // comprueba que cualquier otro dígito se rechaza.
        var valid = Enumerable.Range(0, 10).Select(digit => $"GODE561231GR{digit}").Concat(new[] { "GODE561231GRA" })
            .Where(candidate => ListingRules.IsValidRfc(candidate, company: false))
            .ToArray();

        Assert.Single(valid);
    }

    [Theory]
    [InlineData("+527731234567", true)]
    [InlineData("527731234567", false)]
    [InlineData("+52773123456", false)]
    [InlineData("+1 7731234567", false)]
    [InlineData(null, false)]
    public void Phone_must_be_mexican_e164(string? phone, bool expected)
    {
        Assert.Equal(expected, ListingRules.IsValidPhone(phone));
    }

    [Fact]
    public void Walking_distance_rounds_km_and_minutes_up()
    {
        var (distance, minutes) = ListingRules.Walking(20.0150, -99.3420, 20.00903, -99.34471);

        Assert.Equal(0.7, distance);
        Assert.Equal(9, minutes);
        Assert.Equal((0, 0), ListingRules.Walking(20, -99, 20, -99));
    }

    [Fact]
    public void Room_status_is_derived_from_its_data()
    {
        var setup = new Room { Price = 2800, Status = RoomStatus.Setup }.WithDerivedStatus();
        var ready = (setup with { SizeM2 = 12, Type = "single", Bathroom = "private", AvailableFrom = new DateOnly(2026, 12, 1), MinContract = "annual" }).WithDerivedStatus();
        var available = new Room { Status = RoomStatus.Available }.WithDerivedStatus();

        Assert.Equal(RoomStatus.Setup, setup.Status);
        Assert.Equal(RoomStatus.Ready, ready.Status);
        Assert.Equal(RoomStatus.Available, available.Status);
    }

    [Fact]
    public void Room_pause_and_resume_only_from_valid_states()
    {
        var available = new Room { Status = RoomStatus.Available };

        Assert.Equal(RoomStatus.Paused, available.Pause(Now).Status);
        Assert.Equal(RoomStatus.Available, available.Pause(Now).Resume(Now).Status);
        Assert.Equal("room_state", Assert.Throws<ConflictException>(() => new Room { Status = RoomStatus.Ready }.Pause(Now)).Reason);
        Assert.Equal("room_state", Assert.Throws<ConflictException>(() => available.Resume(Now)).Reason);
        Assert.Equal("room_state", Assert.Throws<ConflictException>(() => new Room { Status = RoomStatus.InReview }.EnsureEditable()).Reason);
    }

    [Fact]
    public void Building_can_only_be_submitted_from_draft()
    {
        var draft = new Building { Status = BuildingStatus.Draft };

        Assert.Equal(BuildingStatus.InReview, draft.SubmitForReview(Now).Status);
        Assert.Equal("building_state", Assert.Throws<ConflictException>(() => draft.SubmitForReview(Now).SubmitForReview(Now)).Reason);
        Assert.Equal("building_state", Assert.Throws<ConflictException>(() => new Building { Status = BuildingStatus.Verified }.SubmitForReview(Now)).Reason);
    }

    [Fact]
    public async Task Landlord_service_requires_profile_and_rejects_duplicates()
    {
        var profiles = new InMemoryLandlordProfileRepository();
        var service = new LandlordService(profiles, new LandlordOptions(), new ManualTimeProvider(Now));
        var request = new CreateLandlordProfileRequest { FullName = "Ana", Phone = "+527731234567", RentalMode = "individual", AcceptedTermsVersion = "2026-11" };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RequireAsync("u1", CancellationToken.None));
        var created = await service.CreateAsync("u1", request, CancellationToken.None);
        await service.RequireAsync("u1", CancellationToken.None);

        Assert.Null(created.Rfc);
        Assert.Equal("landlord_exists", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync("u1", request, CancellationToken.None))).Reason);
    }

    [Fact]
    public async Task Room_service_numbers_rooms_by_floor_and_enforces_limits()
    {
        var (rooms, roomService, building) = await SeedAsync();

        var generated = await roomService.GenerateAsync("u1", building, new GenerateRoomsRequest { Floors = 3, RoomsPerFloor = [2, 1, 3] }, CancellationToken.None);

        Assert.Equal(new[] { 101, 102, 201, 301, 302, 303 }, generated.Items.Select(item => item.Number));
        Assert.All(generated.Items, item => Assert.Equal("setup", item.Status));
        Assert.Equal("rooms_limit", (await Assert.ThrowsAsync<ConflictException>(() =>
            roomService.GenerateAsync("u1", building, new GenerateRoomsRequest { Floors = 1, RoomsPerFloor = [30] }, CancellationToken.None))).Reason);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            roomService.GenerateAsync("u1", building, new GenerateRoomsRequest { Floors = 31, RoomsPerFloor = Enumerable.Repeat(1, 31).ToArray() }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            roomService.GenerateAsync("u1", building, new GenerateRoomsRequest { Floors = 4, RoomsPerFloor = [1, 1, 1, 1], Numbering = "letters" }, CancellationToken.None));
        Assert.Equal(6, rooms.Rooms.Count);
    }

    [Fact]
    public async Task Room_service_hides_rooms_of_other_landlords()
    {
        var (rooms, roomService, building) = await SeedAsync();
        await roomService.GenerateAsync("u1", building, new GenerateRoomsRequest { Floors = 1, RoomsPerFloor = [1] }, CancellationToken.None);
        var room = rooms.Rooms.Values.Single();

        await Assert.ThrowsAsync<NotFoundException>(() => roomService.UpdateAsync("u2", room.Id, new UpdateRoomRequest { Price = 3000, Version = 1 }, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => roomService.DuplicateAsync("u2", room.Id, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => roomService.DuplicateAsync("u1", "not-an-id", CancellationToken.None));
    }

    private static async Task<(InMemoryRoomRepository Rooms, RoomService Service, string BuildingId)> SeedAsync()
    {
        var clock = new ManualTimeProvider(Now);
        var profiles = new InMemoryLandlordProfileRepository();
        foreach (var user in new[] { "u1", "u2" })
        {
            await profiles.CreateAsync(new LandlordProfile { UserId = user, FullName = user, Phone = "+527731234567", RentalMode = "individual", AcceptedTermsVersion = "2026-11" }, CancellationToken.None);
        }

        var landlords = new LandlordService(profiles, new LandlordOptions(), clock);
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        var buildingService = new BuildingService(landlords, buildings, rooms, new NoUniversities(), clock);
        var building = await buildings.CreateAsync(new Building { OwnerId = "u1", Name = "Casa", Status = BuildingStatus.Draft, Version = 1 }, CancellationToken.None);
        return (rooms, new RoomService(buildingService, landlords, rooms, clock), building.Id);
    }

    private sealed class NoUniversities : TheHouse.Application.Abstractions.Persistence.IUniversityRepository
    {
        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<University>, long)>((Array.Empty<University>(), 0));

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<University?>(null);

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<University>>(Array.Empty<University>());

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<University>>(Array.Empty<University>());

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
