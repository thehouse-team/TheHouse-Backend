using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-15/NV-17 1.2.0: dominio y casos de uso de favoritos y visitas sobre
// cuartos, con reloj fijo.
public sealed class FavoriteVisitTests
{
    // 2026-11-10 10:00 en Ciudad de México (UTC-06:00).
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 16, 0, 0, TimeSpan.Zero);

    private const string User = "user-a";
    private const string Other = "user-b";

    // --- VisitSchedulePolicy ---------------------------------------

    [Theory]
    [InlineData("2026-11-20T11:00:00-06:00")]
    [InlineData("2026-11-20T17:00:00Z")]
    [InlineData("2026-11-20T17:00:00.000+00:00")]
    public void ParseScheduledAt_accepts_explicit_offsets_and_returns_utc(string value)
    {
        var parsed = VisitSchedulePolicy.ParseScheduledAt(value);

        Assert.Equal(TimeSpan.Zero, parsed.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 17, 0, 0, TimeSpan.Zero), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-11-20T11:00:00")]
    [InlineData("2026-11-20 11:00:00-06:00")]
    [InlineData("20/11/2026 11:00")]
    [InlineData("2026-13-40T11:00:00-06:00")]
    public void ParseScheduledAt_rejects_missing_offset_or_bad_format(string? value)
    {
        Assert.Throws<RequestValidationException>(() => VisitSchedulePolicy.ParseScheduledAt(value));
    }

    [Theory]
    [InlineData("2026-11-10T12:00:00-06:00", null)]          // exactamente 2 h
    [InlineData("2026-11-10T11:45:00-06:00", "too_soon")]    // 1 h 45 min
    [InlineData("2027-01-09T10:00:00-06:00", null)]          // exactamente 60 días
    [InlineData("2027-01-09T10:15:00-06:00", "too_far")]
    [InlineData("2026-11-20T08:00:00-06:00", null)]          // apertura
    [InlineData("2026-11-20T20:00:00-06:00", null)]          // cierre
    [InlineData("2026-11-20T07:45:00-06:00", "outside_hours")]
    [InlineData("2026-11-20T20:15:00-06:00", "outside_hours")]
    [InlineData("2026-11-20T10:10:00-06:00", "not_quarter_hour")]
    [InlineData("2026-11-20T10:15:30-06:00", "not_quarter_hour")]
    public void EnsureAllowed_applies_lead_hours_and_quarter_rules(string value, string? rule)
    {
        var scheduled = VisitSchedulePolicy.ParseScheduledAt(value);

        if (rule is null)
        {
            VisitSchedulePolicy.EnsureAllowed(scheduled, Now);
            return;
        }

        var exception = Assert.Throws<BusinessRuleException>(() => VisitSchedulePolicy.EnsureAllowed(scheduled, Now));
        Assert.Equal(rule, exception.Rule);
    }

    // --- Visit.Cancel ----------------------------------------------

    [Fact]
    public void Cancel_moves_requested_to_cancelled_and_records_history()
    {
        var visit = NewVisit(Now.AddDays(2));

        var cancelled = visit.Cancel("motivo", Now);

        Assert.Equal(VisitStatus.Cancelled, cancelled.Status);
        Assert.Equal(Now, cancelled.CancelledAt);
        Assert.Equal("motivo", cancelled.CancelReason);
        Assert.Equal(
            new[] { VisitStatus.Requested, VisitStatus.Cancelled },
            cancelled.History.Select(entry => entry.Status));
        Assert.Equal(VisitStatus.Requested, visit.Status);
    }

    [Fact]
    public void Cancel_is_idempotent_for_cancelled_visits()
    {
        var cancelled = NewVisit(Now.AddDays(2)).Cancel(null, Now);

        Assert.Same(cancelled, cancelled.Cancel(null, Now.AddMinutes(5)));
    }

    [Fact]
    public void Cancel_rejects_past_visits_and_reserved_states()
    {
        var past = NewVisit(Now.AddMinutes(-1));
        var completed = new Visit { Status = VisitStatus.Completed, ScheduledAt = Now.AddDays(1) };
        var confirmed = new Visit { Status = VisitStatus.Confirmed, ScheduledAt = Now.AddDays(1) };

        Assert.Equal("visit_in_past", Assert.Throws<ConflictException>(() => past.Cancel(null, Now)).Reason);
        Assert.Equal("visit_state", Assert.Throws<ConflictException>(() => completed.Cancel(null, Now)).Reason);

        // NV-17 1.3.0: el estudiante también cancela visitas confirmadas.
        var cancelled = confirmed.Cancel("Ya no puedo", Now);
        Assert.Equal(VisitStatus.Cancelled, cancelled.Status);
        Assert.Equal(VisitActors.Student, cancelled.CancelledBy);
    }

    // --- FavoriteService (1.2.0, cuartos) --------------------------

    [Fact]
    public async Task Favorites_add_is_idempotent_and_requires_a_published_room()
    {
        var world = World();
        var service = new FavoriteService(world.Favorites, world.Catalog, new ManualTimeProvider(Now));

        await service.AddAsync(User, world.RoomIds[0], CancellationToken.None);
        await service.AddAsync(User, world.RoomIds[0], CancellationToken.None);

        Assert.Single(world.Favorites.Items);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.AddAsync(User, "xyz", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(User, "ffffffffffffffffffffffff", CancellationToken.None));

        var paused = world.Rooms.Rooms[world.RoomIds[1]];
        world.Rooms.Rooms[paused.Id] = paused with { Status = RoomStatus.Paused };
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(User, paused.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Favorites_accept_legacy_property_ids_and_report_them()
    {
        var world = World();
        var service = new FavoriteService(world.Favorites, world.Catalog, new ManualTimeProvider(Now));

        await service.AddAsync(User, LegacyProperty, CancellationToken.None);
        var ids = await service.ListIdsAsync(User, CancellationToken.None);
        var list = await service.ListAsync(User, 1, 20, CancellationToken.None);

        Assert.Equal(world.RoomIds[0], Assert.Single(world.Favorites.Items).RoomId);
        Assert.Equal(new[] { world.RoomIds[0] }, ids.RoomIds);
        Assert.Equal(new[] { LegacyProperty }, ids.PropertyIds);
        Assert.Equal(LegacyProperty, Assert.Single(list.Items).PropertyId);

        await service.RemoveAsync(User, LegacyProperty, CancellationToken.None);
        Assert.Empty(world.Favorites.Items);
    }

    [Fact]
    public async Task Unpublished_rooms_are_hidden_but_not_deleted()
    {
        var world = World();
        var service = new FavoriteService(world.Favorites, world.Catalog, new ManualTimeProvider(Now));
        await service.AddAsync(User, world.RoomIds[2], CancellationToken.None);
        var room = world.Rooms.Rooms[world.RoomIds[2]];

        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };
        var hidden = await service.ListAsync(User, 1, 20, CancellationToken.None);
        world.Rooms.Rooms[room.Id] = room;
        var visible = await service.ListAsync(User, 1, 20, CancellationToken.None);

        Assert.Empty(hidden.Items);
        Assert.Equal(0, hidden.TotalItems);
        Assert.Single(world.Favorites.Items);
        Assert.Single(visible.Items);
    }

    [Fact]
    public async Task Favorites_are_scoped_to_the_user_and_limited_to_200()
    {
        var world = World();
        var service = new FavoriteService(world.Favorites, world.Catalog, new ManualTimeProvider(Now));
        await service.AddAsync(User, world.RoomIds[0], CancellationToken.None);

        Assert.Empty((await service.ListIdsAsync(Other, CancellationToken.None)).RoomIds);

        for (var index = 0; index < FavoriteService.MaximumFavorites; index++)
        {
            world.Favorites.Items.Add(new Favorite { UserId = Other, RoomId = $"9999999999999999{index:D8}", CreatedAt = Now });
        }

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(Other, world.RoomIds[1], CancellationToken.None));
        Assert.Equal("favorites_limit", exception.Reason);
    }

    [Fact]
    public async Task Migration_converts_legacy_favorites_and_visits_and_removes_duplicates()
    {
        var world = World();
        world.Favorites.Items.Add(new Favorite { UserId = User, LegacyPropertyId = LegacyProperty, CreatedAt = Now });
        world.Favorites.Items.Add(new Favorite { UserId = Other, LegacyPropertyId = LegacyProperty, CreatedAt = Now });
        world.Favorites.Items.Add(new Favorite { UserId = Other, RoomId = world.RoomIds[0], CreatedAt = Now });
        world.Favorites.Items.Add(new Favorite { UserId = User, LegacyPropertyId = "0000000000000000000000ff", CreatedAt = Now });
        var visit = await world.Visits.CreateAsync(
            new Visit
            {
                UserId = User,
                LegacyPropertyId = LegacyProperty,
                ScheduledAt = Now.AddDays(3),
                Status = VisitStatus.Requested,
                CreatedAt = Now,
                UpdatedAt = Now,
                History = [new VisitHistoryEntry(VisitStatus.Requested, Now, "student")],
            },
            CancellationToken.None);

        var service = new RoomMigrationService(world.Catalog, world.Favorites, world.Visits);
        var (favorites, visits) = await service.MigrateAsync(CancellationToken.None);
        var again = await service.MigrateAsync(CancellationToken.None);

        Assert.Equal(new MigrationCounts(1, 1, 1), favorites);
        Assert.Equal(new MigrationCounts(1, 0, 0), visits);
        Assert.Equal(new MigrationCounts(0, 0, 1), again.Favorites);
        Assert.Equal(world.RoomIds[0], world.Visits.Visits[visit.Id].RoomId);
    }

    // --- VisitService (1.2.0, cuartos) -----------------------------

    [Fact]
    public async Task Visit_create_replays_same_key_with_room_or_legacy_id()
    {
        var world = World();
        var service = Visits(world);
        var key = Guid.NewGuid().ToString();

        var created = await service.CreateAsync(User, key, Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None);
        var replay = await service.CreateAsync(User, key, LegacyRequest(LegacyProperty, "2026-11-20T17:00:00Z"), CancellationToken.None);

        Assert.False(created.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(created.Visit.Id, replay.Visit.Id);
        Assert.Equal(world.RoomIds[0], created.Visit.Room.Id);
        Assert.Equal(LegacyProperty, created.Visit.Property?.Id);
        Assert.Single(world.Visits.Visits);

        var reused = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(User, key, Request(world.RoomIds[1], "2026-11-21T11:00:00-06:00"), CancellationToken.None));
        Assert.Equal("idempotency_key_reused", reused.Reason);
    }

    [Fact]
    public async Task Visit_create_enforces_conflicts_per_room_and_active_limit()
    {
        var world = World();
        var service = Visits(world);
        for (var index = 0; index < 5; index++)
        {
            await service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[index], $"2026-11-2{index}T11:00:00-06:00"), CancellationToken.None);
        }

        Assert.Equal("active_visit_for_property", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), LegacyRequest(LegacyProperty, "2026-11-29T11:00:00-06:00"), CancellationToken.None))).Reason);
        Assert.Equal("schedule_overlap", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[5], "2026-11-20T11:45:00-06:00"), CancellationToken.None))).Reason);
        Assert.Equal("active_visits_limit", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[5], "2026-11-29T11:00:00-06:00"), CancellationToken.None))).Reason);

        var other = await service.CreateAsync(Other, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None);
        Assert.False(other.Replayed);
    }

    [Fact]
    public async Task Visit_create_validates_input_before_side_effects()
    {
        var world = World();
        var service = Visits(world);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, null, Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), new CreateVisitRequest { RoomId = world.RoomIds[0], PropertyId = LegacyProperty, ScheduledAt = "2026-11-20T11:00:00-06:00" }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), new CreateVisitRequest { ScheduledAt = "2026-11-20T11:00:00-06:00" }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00", new string('x', 301)), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request("ffffffffffffffffffffffff", "2026-11-20T11:00:00-06:00"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T21:00:00-06:00"), CancellationToken.None));

        var room = world.Rooms.Rooms[world.RoomIds[1]];
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(room.Id, "2026-11-20T11:00:00-06:00"), CancellationToken.None));

        Assert.Empty(world.Visits.Visits);
        Assert.Empty(world.Visits.Keys);
    }

    [Fact]
    public async Task Visit_create_fails_when_the_user_lock_is_held()
    {
        var world = World();
        var userLock = new InMemoryUserLock();
        var service = new VisitService(world.Visits, world.Catalog, new InMemoryLandlordProfileRepository(), userLock, new ManualTimeProvider(Now));
        await using var held = await userLock.TryAcquireAsync(RateLimitKeys.VisitLock(User), TimeSpan.FromSeconds(5), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None));

        Assert.Equal("visit_in_progress", exception.Reason);
    }

    [Fact]
    public async Task Visits_of_unpublished_rooms_can_still_be_read_and_cancelled_but_not_by_others()
    {
        var world = World();
        var service = Visits(world);
        var created = await service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None);
        var room = world.Rooms.Rooms[world.RoomIds[0]];
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Other, created.Visit.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelAsync(Other, created.Visit.Id, null, CancellationToken.None));
        Assert.Equal(room.Id, (await service.GetAsync(User, created.Visit.Id, CancellationToken.None)).Room.Id);
        Assert.Equal("cancelled", (await service.CancelAsync(User, created.Visit.Id, null, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Visit_cancel_after_the_scheduled_time_is_a_conflict()
    {
        var world = World();
        var clock = new ManualTimeProvider(Now);
        var service = new VisitService(world.Visits, world.Catalog, new InMemoryLandlordProfileRepository(), new InMemoryUserLock(), clock);
        var created = await service.CreateAsync(User, Guid.NewGuid().ToString(), Request(world.RoomIds[0], "2026-11-20T11:00:00-06:00"), CancellationToken.None);

        clock.Now = new DateTimeOffset(2026, 11, 20, 17, 0, 0, TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.CancelAsync(User, created.Visit.Id, null, CancellationToken.None));
        Assert.Equal("visit_in_past", exception.Reason);
    }

    [Fact]
    public async Task Visit_list_validates_status_filter()
    {
        var service = Visits(World());

        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(User, "closed", 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(User, null, 0, 20, CancellationToken.None));
        Assert.Empty((await service.ListAsync(User, "requested", 1, 20, CancellationToken.None)).Items);
    }

    // --- Soportes --------------------------------------------------

    private const string LegacyProperty = "000000000000000000000001";

    private static Visit NewVisit(DateTimeOffset scheduledAt) =>
        new()
        {
            Id = "7a7a7a7a7a7a7a7a00000001",
            UserId = User,
            RoomId = "c0c0c0c0c0c0c0c000000001",
            ScheduledAt = scheduledAt,
            Status = VisitStatus.Requested,
            CreatedAt = Now,
            UpdatedAt = Now,
            History = [new VisitHistoryEntry(VisitStatus.Requested, Now, "student")],
        };

    private static CreateVisitRequest Request(string roomId, string scheduledAt, string? message = null) =>
        new() { RoomId = roomId, ScheduledAt = scheduledAt, Message = message };

    private static CreateVisitRequest LegacyRequest(string propertyId, string scheduledAt) =>
        new() { PropertyId = propertyId, ScheduledAt = scheduledAt };

    private static VisitService Visits(TestWorld world) =>
        new(world.Visits, world.Catalog, new InMemoryLandlordProfileRepository(), new InMemoryUserLock(), new ManualTimeProvider(Now));

    private sealed record TestWorld(
        InMemoryBuildingRepository Buildings,
        InMemoryRoomRepository Rooms,
        InMemoryFavoriteRepository Favorites,
        InMemoryVisitRepository Visits,
        RoomCatalog Catalog,
        IReadOnlyList<string> RoomIds);

    // Seis edificios verificados con un cuarto disponible cada uno; el primero
    // es `demo` y proviene de la propiedad LegacyProperty.
    private static TestWorld World()
    {
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        var roomIds = new List<string>();
        for (var index = 0; index < 6; index++)
        {
            var building = buildings.CreateAsync(
                new Building
                {
                    OwnerId = "owner",
                    Name = $"Casa {index}",
                    Address = new BuildingAddress("Calle", "1", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                    Latitude = 20.01,
                    Longitude = -99.34,
                    Status = BuildingStatus.Verified,
                    Version = 1,
                    Demo = index == 0,
                    DemoSourceId = index == 0 ? LegacyProperty : null,
                },
                CancellationToken.None).GetAwaiter().GetResult();
            var room = rooms.CreateManyAsync(
                [new Room { BuildingId = building.Id, OwnerId = "owner", Number = 101, Floor = 1, Price = 2500, Status = RoomStatus.Available, Version = 1 }],
                CancellationToken.None).GetAwaiter().GetResult()[0];
            roomIds.Add(room.Id);
        }

        return new TestWorld(buildings, rooms, new InMemoryFavoriteRepository(), new InMemoryVisitRepository(), new RoomCatalog(rooms, buildings), roomIds);
    }
}
