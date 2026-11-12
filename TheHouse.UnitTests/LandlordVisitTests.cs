using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-17 1.3.0: el arrendador confirma, reprograma y cancela; la dirección
// exacta solo con la visita confirmada (RNF-02). Reloj fijo:
// 2026-11-10 10:00 en Ciudad de México.
public sealed class LandlordVisitTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 16, 0, 0, TimeSpan.Zero);

    private const string Student = "665f1c2a9b1e4a00000a11ce";
    private const string Owner = "665f1c2a9b1e4a0000000b0b";
    private const string Stranger = "665f1c2a9b1e4a0000000ca1";

    // --- Dominio ---------------------------------------------------

    [Fact]
    public void Confirm_only_from_requested_and_before_the_time()
    {
        var visit = Requested(Now.AddDays(2));

        var confirmed = visit.Confirm(Now);

        Assert.Equal(VisitStatus.Confirmed, confirmed.Status);
        Assert.Equal(Now, confirmed.ConfirmedAt);
        Assert.Equal(new VisitHistoryEntry(VisitStatus.Confirmed, Now, VisitActors.Landlord), confirmed.History[^1]);
        Assert.Equal("visit_state", Assert.Throws<ConflictException>(() => confirmed.Confirm(Now)).Reason);
        Assert.Equal("visit_in_past", Assert.Throws<ConflictException>(() => Requested(Now.AddMinutes(-5)).Confirm(Now)).Reason);
    }

    [Fact]
    public void Reschedule_keeps_the_previous_time_and_leaves_it_confirmed()
    {
        var visit = Requested(Now.AddDays(2));
        var later = Now.AddDays(3);

        var moved = visit.Reschedule(later, "Ese día no puedo", Now);
        var again = moved.Reschedule(later.AddHours(1), null, Now);

        Assert.Equal(VisitStatus.Confirmed, moved.Status);
        Assert.Equal(later, moved.ScheduledAt);
        Assert.Equal(visit.ScheduledAt, moved.PreviousScheduledAt);
        Assert.Equal("Ese día no puedo", moved.RescheduleNote);
        Assert.Equal(VisitStatus.Rescheduled, moved.History[^1].Status);
        Assert.Equal(later, again.PreviousScheduledAt);
        Assert.Equal("visit_state", Assert.Throws<ConflictException>(() => visit.Cancel(null, Now).Reschedule(later, null, Now)).Reason);
    }

    [Fact]
    public void Landlord_cancel_records_who_and_why()
    {
        var cancelled = Requested(Now.AddDays(2)).Confirm(Now).CancelByLandlord("not_available", "Se rentó", Now);

        Assert.Equal(VisitStatus.Cancelled, cancelled.Status);
        Assert.Equal(VisitActors.Landlord, cancelled.CancelledBy);
        Assert.Equal("not_available", cancelled.CancelReason);
        Assert.Equal("Se rentó", cancelled.CancelNote);
        Assert.Equal("visit_state", Assert.Throws<ConflictException>(() => cancelled.CancelByLandlord("other", "x", Now)).Reason);
    }

    [Fact]
    public void Confirmed_visits_count_as_active()
    {
        Assert.True(Requested(Now.AddDays(1)).Confirm(Now).IsActive(Now));
        Assert.False(Requested(Now.AddDays(1)).Cancel(null, Now).IsActive(Now));
    }

    // --- Calendario --------------------------------------------------

    [Fact]
    public async Task Calendar_lists_only_my_rooms_in_range_with_student_summary()
    {
        var world = World(rooms: 2);
        await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomIds[0]), CancellationToken.None);
        await world.Visits.CreateAsync(Requested(Now.AddDays(5), world.RoomIds[1]), CancellationToken.None);
        await world.Visits.CreateAsync(Requested(Now.AddDays(40), world.RoomIds[0]), CancellationToken.None);
        await world.Landlords.CreateAsync(Landlord(Stranger), CancellationToken.None);

        var defaultRange = await world.Service.CalendarAsync(Owner, null, null, null, null, 1, 20, CancellationToken.None);
        var byRoom = await world.Service.CalendarAsync(Owner, null, null, null, world.RoomIds[1], 1, 20, CancellationToken.None);
        var stranger = await world.Service.CalendarAsync(Stranger, null, null, null, null, 1, 20, CancellationToken.None);

        Assert.Equal(2, defaultRange.TotalItems);
        Assert.True(defaultRange.Items[0].ScheduledAt < defaultRange.Items[1].ScheduledAt);
        Assert.Equal(new VisitStudentSummary("Ana", "Universidad Tecnológica de Tula-Tepeji"), defaultRange.Items[0].Student);
        Assert.Equal(world.RoomIds[1], Assert.Single(byRoom.Items).Room.Id);
        Assert.Empty(stranger.Items);
    }

    [Theory]
    [InlineData("2026-11-12T00:00:00-06:00", "2026-11-11T00:00:00-06:00")]
    [InlineData("2026-11-12T00:00:00-06:00", "2027-01-20T00:00:00-06:00")]
    [InlineData("2026-11-12", null)]
    public async Task Calendar_validates_the_range(string from, string? to)
    {
        var world = World();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.CalendarAsync(Owner, from, to, null, null, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Landlord_mode_is_required_and_foreign_visits_are_404()
    {
        var world = World();
        var visit = await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomId), CancellationToken.None);
        await world.Landlords.CreateAsync(Landlord(Stranger), CancellationToken.None);

        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(
            () => world.Service.CalendarAsync(Student, null, null, null, null, 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.ConfirmAsync(Stranger, visit.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.GetAsync(Stranger, visit.Id, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.GetAsync(Owner, "no-es-id", CancellationToken.None));

        Assert.Equal("landlord_required", forbidden.Reason);
    }

    // --- Acciones ----------------------------------------------------

    [Fact]
    public async Task Confirm_requires_a_published_room()
    {
        var world = World();
        var visit = await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomId), CancellationToken.None);
        var room = world.Rooms.Rooms[world.RoomId];
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };

        var exception = await Assert.ThrowsAsync<ConflictException>(() => world.Service.ConfirmAsync(Owner, visit.Id, CancellationToken.None));
        var cancelled = await world.Service.CancelAsync(Owner, visit.Id, new LandlordCancelVisitRequest { Reason = "not_available" }, CancellationToken.None);

        Assert.Equal("visit_state", exception.Reason);
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal(new VisitCancellation("landlord", "not_available", null), cancelled.Cancellation);
    }

    [Fact]
    public async Task Reschedule_validates_the_schedule_and_the_students_other_visits()
    {
        var world = World(rooms: 2);
        var visit = await world.Visits.CreateAsync(Requested(At(2, 11), world.RoomIds[0]), CancellationToken.None);
        await world.Visits.CreateAsync(Requested(At(3, 11), world.RoomIds[1]), CancellationToken.None);

        var outside = await Assert.ThrowsAsync<BusinessRuleException>(
            () => world.Service.RescheduleAsync(Owner, visit.Id, new RescheduleVisitRequest { ScheduledAt = Local(3, 21) }, CancellationToken.None));
        var overlap = await Assert.ThrowsAsync<ConflictException>(
            () => world.Service.RescheduleAsync(Owner, visit.Id, new RescheduleVisitRequest { ScheduledAt = Local(3, 11, 30) }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.RescheduleAsync(Owner, visit.Id, new RescheduleVisitRequest { ScheduledAt = Local(4, 11), Note = new string('n', 301) }, CancellationToken.None));
        var moved = await world.Service.RescheduleAsync(Owner, visit.Id, new RescheduleVisitRequest { ScheduledAt = Local(4, 12), Note = "Mejor el jueves" }, CancellationToken.None);

        Assert.Equal("outside_hours", outside.Rule);
        Assert.Equal("student_schedule_overlap", overlap.Reason);
        Assert.Equal("confirmed", moved.Status);
        Assert.Equal(At(2, 11), moved.PreviousScheduledAt);
        Assert.Equal(At(4, 12), moved.ScheduledAt);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("spam", null)]
    [InlineData("other", null)]
    [InlineData("other", "   ")]
    public async Task Landlord_cancel_validates_reason_and_note(string? reason, string? note)
    {
        var world = World();
        var visit = await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomId), CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.CancelAsync(Owner, visit.Id, new LandlordCancelVisitRequest { Reason = reason, Note = note }, CancellationToken.None));
        Assert.Equal(VisitStatus.Requested, world.Visits.Visits[visit.Id].Status);
    }

    [Fact]
    public async Task A_concurrent_change_is_reported_instead_of_overwritten()
    {
        var world = World();
        var visit = await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomId), CancellationToken.None);
        var racing = new RacingVisitRepository(world.Visits);
        var service = new LandlordVisitService(racing, world.Rooms, world.Buildings, world.Landlords, world.Profiles, new UniversityStub(), new ManualTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.ConfirmAsync(Owner, visit.Id, CancellationToken.None));

        Assert.Equal("visit_state", exception.Reason);
        Assert.Equal(VisitStatus.Cancelled, world.Visits.Visits[visit.Id].Status);
    }

    // --- Vista del estudiante ----------------------------------------

    [Fact]
    public async Task Student_sees_the_exact_address_only_when_confirmed()
    {
        var world = World();
        var students = new VisitService(world.Visits, new RoomCatalog(world.Rooms, world.Buildings), world.Landlords, new InMemoryUserLock(), new ManualTimeProvider(Now));
        var visit = await world.Visits.CreateAsync(Requested(Now.AddDays(2), world.RoomId), CancellationToken.None);

        var requested = await students.GetAsync(Student, visit.Id, CancellationToken.None);
        await world.Service.ConfirmAsync(Owner, visit.Id, CancellationToken.None);
        var confirmed = await students.GetAsync(Student, visit.Id, CancellationToken.None);
        var cancelled = await students.CancelAsync(Student, visit.Id, "Ya encontré", CancellationToken.None);

        Assert.Null(requested.Address);
        Assert.Null(requested.Location);
        Assert.Equal("Laura", requested.Landlord.Name);
        Assert.Equal(new VisitAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"), confirmed.Address);
        Assert.Equal(new VisitLocation(20.0512, -99.3401), confirmed.Location);
        Assert.Equal(Now, confirmed.ConfirmedAt);
        Assert.Null(cancelled.Address);
        Assert.Equal(new VisitCancellation("student", "Ya encontré", null), cancelled.Cancellation);
    }

    // --- Soportes ----------------------------------------------------

    private static DateTimeOffset At(int days, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 10, hour, minute, 0, TimeSpan.FromHours(-6)).AddDays(days).ToUniversalTime();

    private static string Local(int days, int hour, int minute = 0) =>
        new DateTimeOffset(2026, 11, 10, hour, minute, 0, TimeSpan.FromHours(-6)).AddDays(days).ToString("yyyy-MM-dd'T'HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);

    private static Visit Requested(DateTimeOffset scheduledAt, string roomId = "c0c0c0c0c0c0c0c000000001") =>
        new()
        {
            Id = "7a7a7a7a7a7a7a7a00000001",
            UserId = Student,
            RoomId = roomId,
            ScheduledAt = scheduledAt,
            Status = VisitStatus.Requested,
            CreatedAt = Now,
            UpdatedAt = Now,
            History = [new VisitHistoryEntry(VisitStatus.Requested, Now, VisitActors.Student)],
        };

    private static LandlordProfile Landlord(string userId) =>
        new()
        {
            UserId = userId,
            FullName = userId == Owner ? "Laura Gómez" : "Pedro Ruiz",
            Phone = "+527711234567",
            RentalMode = "individual",
            AcceptedTermsVersion = "1.0",
            AcceptedTermsAt = Now,
            CreatedAt = Now,
        };

    private sealed record TestWorld(
        LandlordVisitService Service,
        InMemoryVisitRepository Visits,
        InMemoryRoomRepository Rooms,
        InMemoryBuildingRepository Buildings,
        InMemoryLandlordProfileRepository Landlords,
        InMemoryProfileRepository Profiles,
        IReadOnlyList<string> RoomIds)
    {
        public string RoomId => RoomIds[0];
    }

    private static TestWorld World(int rooms = 1)
    {
        var buildings = new InMemoryBuildingRepository();
        var roomStore = new InMemoryRoomRepository();
        var landlords = new InMemoryLandlordProfileRepository();
        var profiles = new InMemoryProfileRepository();
        var visits = new InMemoryVisitRepository();
        landlords.CreateAsync(Landlord(Owner), CancellationToken.None).GetAwaiter().GetResult();
        profiles.Profiles[Student] = new UserProfile { UserId = Student, Name = "Ana", UniversityId = UniversityStub.UtttId };

        var building = buildings.CreateAsync(
            new Building
            {
                OwnerId = Owner,
                Name = "Casa Azul",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Latitude = 20.0512,
                Longitude = -99.3401,
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None).GetAwaiter().GetResult();
        var ids = new List<string>();
        for (var index = 0; index < rooms; index++)
        {
            var room = roomStore.CreateManyAsync(
                [new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101 + index, Floor = 1, Price = 2500, Status = RoomStatus.Available, Version = 1 }],
                CancellationToken.None).GetAwaiter().GetResult()[0];
            ids.Add(room.Id);
        }

        var service = new LandlordVisitService(visits, roomStore, buildings, landlords, profiles, new UniversityStub(), new ManualTimeProvider(Now));
        return new TestWorld(service, visits, roomStore, buildings, landlords, profiles, ids);
    }

    // Simula que el estudiante cancela justo antes de que el arrendador guarde.
    private sealed class RacingVisitRepository(InMemoryVisitRepository inner) : IVisitRepository
    {
        public Task<Visit> CreateAsync(Visit visit, CancellationToken cancellationToken) => inner.CreateAsync(visit, cancellationToken);

        public Task<Visit?> GetAsync(string id, CancellationToken cancellationToken) => inner.GetAsync(id, cancellationToken);

        public Task UpdateAsync(Visit visit, CancellationToken cancellationToken) => inner.UpdateAsync(visit, cancellationToken);

        public async Task<bool> ReplaceIfUnchangedAsync(Visit visit, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
        {
            var current = inner.Visits[visit.Id];
            inner.Visits[visit.Id] = current.Cancel("carrera", Now.AddSeconds(1));
            return await inner.ReplaceIfUnchangedAsync(visit, expectedUpdatedAt, cancellationToken);
        }

        public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListForRoomsAsync(IReadOnlyCollection<string> roomIds, DateTimeOffset from, DateTimeOffset to, VisitStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
            inner.ListForRoomsAsync(roomIds, from, to, status, page, pageSize, cancellationToken);

        public Task<IReadOnlyList<Visit>> ListActiveAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => inner.ListActiveAsync(userId, now, cancellationToken);

        public Task<(IReadOnlyList<Visit> Items, long TotalItems)> ListAsync(string userId, VisitStatus? status, int page, int pageSize, CancellationToken cancellationToken) =>
            inner.ListAsync(userId, status, page, pageSize, cancellationToken);

        public Task<VisitIdempotencyRecord?> FindIdempotencyAsync(string userId, string key, CancellationToken cancellationToken) => inner.FindIdempotencyAsync(userId, key, cancellationToken);

        public Task<MigrationCounts> MigrateLegacyAsync(IReadOnlyDictionary<string, string> roomByProperty, CancellationToken cancellationToken) => inner.MigrateLegacyAsync(roomByProperty, cancellationToken);

        public Task SaveIdempotencyAsync(VisitIdempotencyRecord record, CancellationToken cancellationToken) => inner.SaveIdempotencyAsync(record, cancellationToken);
    }

    private sealed class UniversityStub : IUniversityRepository
    {
        public const string UtttId = "6650000000000000000000aa";

        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<University?>(id == UtttId ? new University { Id = UtttId, Code = "UTTT", Name = "Universidad Tecnológica de Tula-Tepeji" } : null);

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
