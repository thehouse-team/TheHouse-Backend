using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-25: reglas de rango, conversión, vistas por persona y día, conteos del
// resumen y las 6 sugerencias (cada una se dispara y no se dispara).
public sealed class LandlordStatsTests
{
    private const string Owner = "6650000000000000000000b0";
    private const string Student = "6650000000000000000000a1";
    private const string Other = "6650000000000000000000a2";
    private const string Staff = "6650000000000000000000c1";
    private const string Rival = "6650000000000000000000b9";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 17);

    [Theory]
    [InlineData(null, null, "2026-10-19", "2026-11-17")]
    [InlineData("2026-11-01", "2026-11-10", "2026-11-01", "2026-11-10")]
    [InlineData("2026-08-20", "2026-11-17", "2026-08-20", "2026-11-17")]
    public void Range_defaults_to_30_days_and_accepts_up_to_90(string? from, string? to, string start, string end)
    {
        var (first, last) = LandlordStatsRules.Range(from, to, Today);

        Assert.Equal(start, LandlordStatsRules.DayText(first));
        Assert.Equal(end, LandlordStatsRules.DayText(last));
    }

    [Theory]
    [InlineData("2026-08-19", "2026-11-17")]
    [InlineData("2026-11-10", "2026-11-01")]
    [InlineData("2026-11-01", "2026-11-18")]
    [InlineData("2025-11-01", "2025-11-10")]
    [InlineData("17/11/2026", null)]
    public void Range_rejects_invalid_or_too_long(string? from, string? to) =>
        Assert.Throws<RequestValidationException>(() => LandlordStatsRules.Range(from, to, Today));

    [Fact]
    public void Conversion_median_and_hash_rules()
    {
        Assert.Null(LandlordStatsRules.Conversion(3, 0));
        Assert.Equal(0.333, LandlordStatsRules.Conversion(1, 3));
        Assert.Equal(2500m, LandlordStatsRules.Median([3000, 2000, 2500]));
        Assert.Equal(2250m, LandlordStatsRules.Median([2000, 2500, 3000, 1500]));

        var monday = LandlordStatsRules.ActorHash([1, 2, 3], Student);
        Assert.Equal(64, monday.Length);
        Assert.Equal(monday, LandlordStatsRules.ActorHash([1, 2, 3], Student));
        Assert.NotEqual(monday, LandlordStatsRules.ActorHash([4, 5, 6], Student));
        Assert.DoesNotContain(Student, monday, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_view_per_person_and_day_without_owner_or_staff()
    {
        var world = await WorldAsync();
        var room = world.Room;

        Assert.True(await world.Views.RecordAsync(room, Student, CancellationToken.None));
        Assert.False(await world.Views.RecordAsync(room, Student, CancellationToken.None));
        Assert.True(await world.Views.RecordAsync(room, Other, CancellationToken.None));
        Assert.False(await world.Views.RecordAsync(room, Owner, CancellationToken.None));
        await world.Moderation.GrantAsync(Staff, StaffRoles.Admin, Owner, Now, CancellationToken.None);
        Assert.False(await world.Views.RecordAsync(room, Staff, CancellationToken.None));

        world.Clock.Now = Now.AddDays(1);
        Assert.True(await world.Views.RecordAsync(room, Student, CancellationToken.None));

        var stored = world.RoomViews.Items;
        Assert.Equal(3, stored.Count);
        Assert.Equal(["2026-11-17", "2026-11-17", "2026-11-18"], stored.Select(item => item.Day));
        Assert.NotEqual(stored[0].ActorHash, stored[2].ActorHash);
        Assert.All(stored, item => Assert.DoesNotContain(Student, item.ActorHash, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Summary_counts_activity_in_range_previous_period_and_daily_series()
    {
        var world = await WorldAsync();
        world.RoomViews.Seed(world.Room, "2026-11-16", 4);
        world.RoomViews.Seed(world.Room, "2026-11-17", 6);
        world.RoomViews.Seed(world.Room, "2026-10-10", 5);
        await world.Favorites.AddAsync(new Favorite { UserId = Student, RoomId = world.Room.Id, CreatedAt = Now.AddDays(-1) }, CancellationToken.None);
        await world.Favorites.AddAsync(new Favorite { UserId = Other, RoomId = world.Room.Id, CreatedAt = Now.AddDays(-60) }, CancellationToken.None);
        await AddRequestAsync(world, Student, Now.AddDays(-1), RentalRequestStatus.Accepted);
        await AddRequestAsync(world, Other, Now.AddDays(-40), RentalRequestStatus.Pending);

        var summary = await world.Stats.SummaryAsync(Owner, null, null, null, CancellationToken.None);

        Assert.Equal("2026-10-19", summary.From);
        Assert.Equal(30, summary.Daily.Count);
        Assert.Equal(10, summary.Totals.Views);
        Assert.Equal(1, summary.Totals.Favorites);
        Assert.Equal(2, summary.FavoritesNow);
        Assert.Equal(1, summary.Totals.Requests);
        Assert.Equal(1, summary.Totals.AcceptedRequests);
        Assert.Equal(0.1, summary.Totals.Conversion);
        Assert.Equal(5, summary.Previous.Views);
        Assert.Equal(1, summary.Previous.Requests);
        Assert.Equal(6, summary.Daily[^1].Views);
        Assert.Equal(1, summary.Daily[^2].Requests);

        await Assert.ThrowsAsync<NotFoundException>(() => world.Stats.SummaryAsync(Owner, null, null, "6650000000000000000000ff", CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => world.Stats.SummaryAsync(Student, null, null, null, CancellationToken.None));
        var rival = await world.Stats.SummaryAsync(Rival, null, null, null, CancellationToken.None);
        Assert.Equal(0, rival.Totals.Views);
    }

    [Fact]
    public async Task Rooms_are_ordered_by_views_and_paginated()
    {
        var world = await WorldAsync();
        var second = (await world.Rooms.CreateManyAsync([NewRoom(world.Room.BuildingId, 102, 2500)], CancellationToken.None))[0];
        world.RoomViews.Seed(second, "2026-11-17", 3);
        world.RoomViews.Seed(world.Room, "2026-11-17", 1);

        var page = await world.Stats.RoomsAsync(Owner, null, null, null, 1, 1, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(second.Id, item.RoomId);
        Assert.Equal(3, item.Views);
        Assert.Equal("Casa Verde", item.BuildingName);
        Assert.Null(item.Rating);
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task Recommendations_fire_only_when_their_rule_applies()
    {
        var world = await WorldAsync(complete: false);
        world.RoomViews.Seed(world.Room, "2026-11-10", 31);
        await AddRequestAsync(world, Student, Now.AddDays(-3), RentalRequestStatus.Pending, room: world.Neighbors[0]);
        await world.Visits.CreateAsync(
            new Visit { Id = "6650000000000000000000e1", UserId = Student, RoomId = world.Room.Id, ScheduledAt = Now.AddDays(3), Status = VisitStatus.Requested, CreatedAt = Now.AddDays(-2), UpdatedAt = Now.AddDays(-2) },
            CancellationToken.None);

        var codes = (await world.Stats.RecommendationsAsync(Owner, null, CancellationToken.None)).Items
            .Where(item => item.RoomId == world.Room.Id)
            .Select(item => item.Code)
            .ToArray();

        Assert.Equal(["add_photos", "pending_visits", "missing_details", "price_above_median", "views_without_requests"], codes);

        var calm = await WorldAsync();
        var calmCodes = (await calm.Stats.RecommendationsAsync(Owner, null, CancellationToken.None)).Items.Where(item => item.RoomId == calm.Room.Id).Select(item => item.Code);
        Assert.Equal(["add_photos"], calmCodes);
    }

    [Fact]
    public async Task Pending_requests_are_flagged_after_48_hours()
    {
        var world = await WorldAsync();
        await AddRequestAsync(world, Student, Now.AddHours(-47), RentalRequestStatus.Pending);
        Assert.DoesNotContain("pending_requests", (await world.Stats.RecommendationsAsync(Owner, null, CancellationToken.None)).Items.Select(item => item.Code));

        await AddRequestAsync(world, Other, Now.AddHours(-49), RentalRequestStatus.Pending);
        var tip = Assert.Single((await world.Stats.RecommendationsAsync(Owner, null, CancellationToken.None)).Items, item => item.Code == "pending_requests");
        Assert.Equal("high", tip.Priority);
        Assert.Contains("1 solicitud espera", tip.Detail, StringComparison.Ordinal);
    }

    // --- Apoyo -------------------------------------------------------

    private sealed record World(
        LandlordStatsService Stats,
        RoomViewService Views,
        InMemoryRoomViewRepository RoomViews,
        InMemoryRoomRepository Rooms,
        InMemoryFavoriteRepository Favorites,
        InMemoryVisitRepository Visits,
        InMemoryRentalRequestRepository Requests,
        InMemoryModerationRepositories Moderation,
        ManualTimeProvider Clock,
        Room Room,
        IReadOnlyList<Room> Neighbors);

    private static async Task<World> WorldAsync(bool complete = true)
    {
        var clock = new ManualTimeProvider(Now);
        var landlords = new InMemoryLandlordProfileRepository();
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        await landlords.CreateAsync(new LandlordProfile { UserId = Owner, FullName = "Bob Pérez", Phone = "+527711234567", RentalMode = "individual", AcceptedTermsVersion = "1.0", AcceptedTermsAt = Now, CreatedAt = Now }, CancellationToken.None);
        await landlords.CreateAsync(new LandlordProfile { UserId = Rival, FullName = "Rita Ruiz", Phone = "+527711234568", RentalMode = "individual", AcceptedTermsVersion = "1.0", AcceptedTermsAt = Now, CreatedAt = Now }, CancellationToken.None);
        var building = await buildings.CreateAsync(NewBuilding(Owner, "Casa Verde"), CancellationToken.None);
        var neighbor = await buildings.CreateAsync(NewBuilding(Rival, "Casa Azul"), CancellationToken.None);
        var room = NewRoom(building.Id, 101, complete ? 2600 : 4000);
        if (!complete)
        {
            room = new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101, Floor = 1, Price = 4000, Status = RoomStatus.Available, Version = 1 };
        }

        var created = (await rooms.CreateManyAsync([room], CancellationToken.None))[0];
        var neighbors = await rooms.CreateManyAsync(
            [.. new[] { 2400, 2500, 2600, 2700, 2800 }.Select((price, index) => NewRoom(neighbor.Id, 201 + index, price, Rival))],
            CancellationToken.None);

        var views = new InMemoryRoomViewRepository();
        var favorites = new InMemoryFavoriteRepository();
        var visits = new InMemoryVisitRepository();
        var requests = new InMemoryRentalRequestRepository();
        var moderation = new InMemoryModerationRepositories();
        var media = new MediaPort(new InMemoryMediaRepository(), clock);
        var stats = new LandlordStatsService(landlords, buildings, rooms, views, favorites, visits, requests, new InMemoryAgreementRepository(), new InMemoryReviewRepository(), clock, media);
        var recorder = new RoomViewService(views, new InMemoryViewSaltStore(), moderation, clock);
        return new World(stats, recorder, views, rooms, favorites, visits, requests, moderation, clock, created, neighbors);
    }

    private static Building NewBuilding(string owner, string name) =>
        new()
        {
            OwnerId = owner,
            Name = name,
            Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
            Latitude = 20.0512,
            Longitude = -99.3401,
            NearestUniversityId = "6650000000000000000000d1",
            Status = BuildingStatus.Verified,
            Version = 1,
        };

    private static Room NewRoom(string buildingId, int number, int price, string owner = Owner) =>
        new()
        {
            BuildingId = buildingId,
            OwnerId = owner,
            Number = number,
            Floor = 1,
            Price = price,
            SizeM2 = 12,
            Furniture = ["bed"],
            AvailableFrom = new DateOnly(2026, 12, 1),
            Status = RoomStatus.Available,
            Version = 1,
        };

    private static Task AddRequestAsync(World world, string student, DateTimeOffset at, RentalRequestStatus status, Room? room = null)
    {
        var target = room ?? world.Room;
        return world.Requests.CreateAsync(
            new RentalRequest
            {
                RoomId = target.Id,
                BuildingId = target.BuildingId,
                StudentId = student,
                OwnerId = target.OwnerId,
                Message = "Hola",
                Status = status,
                CreatedAt = at,
                UpdatedAt = at,
            },
            CancellationToken.None);
    }
}
