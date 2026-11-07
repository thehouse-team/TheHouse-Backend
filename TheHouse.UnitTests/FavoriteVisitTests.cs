using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-15/NV-17: dominio y casos de uso de favoritos y visitas con reloj fijo.
public sealed class FavoriteVisitTests
{
    // 2026-11-10 10:00 en Ciudad de México (UTC-06:00).
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 16, 0, 0, TimeSpan.Zero);

    private const string User = "user-a";
    private const string Other = "user-b";
    private const string PropertyA = "0123456789876543000000a1";
    private const string PropertyB = "0123456789876543000000b2";

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
        var confirmed = new Visit { Status = VisitStatus.Confirmed, ScheduledAt = Now.AddDays(1) };

        Assert.Equal("visit_in_past", Assert.Throws<ConflictException>(() => past.Cancel(null, Now)).Reason);
        Assert.Equal("visit_state", Assert.Throws<ConflictException>(() => confirmed.Cancel(null, Now)).Reason);
    }

    // --- FavoriteService -------------------------------------------

    [Fact]
    public async Task Favorites_add_is_idempotent_and_validates_property()
    {
        var (service, favorites, _) = Favorites();

        await service.AddAsync(User, PropertyA, CancellationToken.None);
        await service.AddAsync(User, PropertyA, CancellationToken.None);

        Assert.Single(favorites.Items);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.AddAsync(User, "xyz", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(User, "0123456789876543ffffffff", CancellationToken.None));
    }

    [Fact]
    public async Task Favorites_are_scoped_to_the_user()
    {
        var (service, _, _) = Favorites();
        await service.AddAsync(User, PropertyA, CancellationToken.None);

        var mine = await service.ListIdsAsync(User, CancellationToken.None);
        var theirs = await service.ListIdsAsync(Other, CancellationToken.None);

        Assert.Equal(new[] { PropertyA }, mine.PropertyIds);
        Assert.Empty(theirs.PropertyIds);
    }

    [Fact]
    public async Task Favorites_list_orders_newest_first_and_skips_orphans()
    {
        var (service, favorites, properties) = Favorites();
        favorites.Items.Add(new Favorite { UserId = User, PropertyId = PropertyA, CreatedAt = Now.AddMinutes(-10) });
        favorites.Items.Add(new Favorite { UserId = User, PropertyId = PropertyB, CreatedAt = Now });
        favorites.Items.Add(new Favorite { UserId = User, PropertyId = "0123456789876543ffffffff", CreatedAt = Now.AddMinutes(-5) });

        var list = await service.ListAsync(User, 1, 20, CancellationToken.None);

        Assert.Equal(new[] { PropertyB, PropertyA }, list.Items.Select(item => item.PropertyId));
        Assert.Equal(2, list.TotalItems);
        Assert.Equal(2, favorites.Items.Count);
        Assert.Equal(2, properties.Properties.Count);
    }

    [Fact]
    public async Task Favorites_limit_is_200()
    {
        var (service, favorites, _) = Favorites();
        for (var index = 0; index < FavoriteService.MaximumFavorites; index++)
        {
            favorites.Items.Add(new Favorite { UserId = User, PropertyId = $"0123456789876543{index:D8}", CreatedAt = Now });
        }

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.AddAsync(User, PropertyA, CancellationToken.None));
        Assert.Equal("favorites_limit", exception.Reason);
    }

    // --- VisitService ----------------------------------------------

    [Fact]
    public async Task Visit_create_replays_same_key_and_rejects_reused_key()
    {
        var (service, visits, _) = Visits();
        var key = Guid.NewGuid().ToString();

        var created = await service.CreateAsync(User, key, Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None);
        var replay = await service.CreateAsync(User, key, Request(PropertyA, "2026-11-20T17:00:00Z"), CancellationToken.None);

        Assert.False(created.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(created.Visit.Id, replay.Visit.Id);
        Assert.Equal("requested", created.Visit.Status);
        Assert.Equal(VisitSchedulePolicy.TimeZoneId, created.Visit.TimeZone);
        Assert.Single(visits.Visits);

        var reused = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(User, key, Request(PropertyB, "2026-11-21T11:00:00-06:00"), CancellationToken.None));
        Assert.Equal("idempotency_key_reused", reused.Reason);
    }

    [Fact]
    public async Task Visit_create_enforces_conflicts_and_active_limit()
    {
        var (service, visits, properties) = Visits();
        for (var index = 0; index < 5; index++)
        {
            properties.Properties.Add(new Property { Id = $"0123456789876543{index:D8}", Title = "P", City = "Tula" });
            await service.CreateAsync(User, Guid.NewGuid().ToString(), Request($"0123456789876543{index:D8}", $"2026-11-2{index}T11:00:00-06:00"), CancellationToken.None);
        }

        Assert.Equal(
            "active_visit_for_property",
            (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request("012345678987654300000000", "2026-11-29T11:00:00-06:00"), CancellationToken.None))).Reason);
        Assert.Equal(
            "schedule_overlap",
            (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:45:00-06:00"), CancellationToken.None))).Reason);
        Assert.Equal(
            "active_visits_limit",
            (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-29T11:00:00-06:00"), CancellationToken.None))).Reason);

        // Otro usuario no se ve afectado por los límites del primero.
        var other = await service.CreateAsync(Other, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None);
        Assert.False(other.Replayed);
        Assert.Equal(6, visits.Visits.Count);
    }

    [Fact]
    public async Task Visit_create_validates_input_before_side_effects()
    {
        var (service, visits, _) = Visits();

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, null, Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, "abc", Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:00:00-06:00", new string('x', 301)), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request("0123456789876543ffffffff", "2026-11-20T11:00:00-06:00"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T21:00:00-06:00"), CancellationToken.None));

        Assert.Empty(visits.Visits);
        Assert.Empty(visits.Keys);
    }

    [Fact]
    public async Task Visit_create_fails_when_the_user_lock_is_held()
    {
        var visits = new InMemoryVisitRepository();
        var userLock = new InMemoryUserLock();
        var service = new VisitService(visits, SeededProperties(), userLock, new ManualTimeProvider(Now));
        await using var held = await userLock.TryAcquireAsync(RateLimitKeys.VisitLock(User), TimeSpan.FromSeconds(5), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None));

        Assert.Equal("visit_in_progress", exception.Reason);
        Assert.Empty(visits.Visits);
    }

    [Fact]
    public async Task Visit_get_and_cancel_hide_other_users_visits()
    {
        var (service, _, _) = Visits();
        var created = await service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Other, created.Visit.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelAsync(Other, created.Visit.Id, null, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.GetAsync(User, "nope", CancellationToken.None));
        Assert.Equal("requested", (await service.GetAsync(User, created.Visit.Id, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Visit_cancel_after_the_scheduled_time_is_a_conflict()
    {
        var clock = new ManualTimeProvider(Now);
        var visits = new InMemoryVisitRepository();
        var service = new VisitService(visits, SeededProperties(), new InMemoryUserLock(), clock);
        var created = await service.CreateAsync(User, Guid.NewGuid().ToString(), Request(PropertyA, "2026-11-20T11:00:00-06:00"), CancellationToken.None);

        clock.Now = new DateTimeOffset(2026, 11, 20, 17, 0, 0, TimeSpan.Zero);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelAsync(User, created.Visit.Id, null, CancellationToken.None));
        Assert.Equal("visit_in_past", exception.Reason);
    }

    [Fact]
    public async Task Visit_list_validates_status_filter()
    {
        var (service, _, _) = Visits();

        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(User, "confirmed", 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(User, null, 0, 20, CancellationToken.None));
        Assert.Empty((await service.ListAsync(User, "requested", 1, 20, CancellationToken.None)).Items);
    }

    // --- Soportes --------------------------------------------------

    private static Visit NewVisit(DateTimeOffset scheduledAt) =>
        new()
        {
            Id = "7a7a7a7a7a7a7a7a00000001",
            UserId = User,
            PropertyId = PropertyA,
            ScheduledAt = scheduledAt,
            Status = VisitStatus.Requested,
            CreatedAt = Now,
            UpdatedAt = Now,
            History = [new VisitHistoryEntry(VisitStatus.Requested, Now, "student")],
        };

    private static CreateVisitRequest Request(string propertyId, string scheduledAt, string? message = null) =>
        new() { PropertyId = propertyId, ScheduledAt = scheduledAt, Message = message };

    private static (FavoriteService, InMemoryFavoriteRepository, FakeProperties) Favorites()
    {
        var favorites = new InMemoryFavoriteRepository();
        var properties = SeededProperties();
        return (new FavoriteService(favorites, properties, new ManualTimeProvider(Now)), favorites, properties);
    }

    private static (VisitService, InMemoryVisitRepository, FakeProperties) Visits()
    {
        var visits = new InMemoryVisitRepository();
        var properties = SeededProperties();
        properties.Properties.Add(new Property { Id = "012345678987654300000000", Title = "P0", City = "Tula" });
        return (new VisitService(visits, properties, new InMemoryUserLock(), new ManualTimeProvider(Now)), visits, properties);
    }

    private static FakeProperties SeededProperties()
    {
        var properties = new FakeProperties();
        properties.Properties.Add(new Property { Id = PropertyA, Title = "Depa A", City = "Tula" });
        properties.Properties.Add(new Property { Id = PropertyB, Title = "Cuarto B", City = "Tepeji" });
        return properties;
    }

    private sealed class FakeProperties : IPropertyRepository
    {
        public List<Property> Properties { get; } = [];

        public Task<(IReadOnlyList<Property> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<Property>, long)>((Properties, Properties.Count));

        public Task<Property?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Properties.FirstOrDefault(property => property.Id == id));

        public Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Property>>(Properties.Where(property => ids.Contains(property.Id)).ToList());

        public Task UpsertSeedAsync(IReadOnlyCollection<Property> properties, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
