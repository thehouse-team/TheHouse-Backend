using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Rentals;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-18: convenio, código de cierre, aceptación con el cuarto, efectos al
// rentar, Mi renta y fin de renta. Reloj fijo: 2026-11-12 10:00 en Ciudad de
// México.
public sealed class RentalTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 16, 0, 0, TimeSpan.Zero);

    private const string Student = "665f1c2a9b1e4a00000a11ce";
    private const string Owner = "665f1c2a9b1e4a0000000b0b";
    private const string Other = "665f1c2a9b1e4a0000000ca1";

    // --- Dominio ---------------------------------------------------

    [Fact]
    public void Pending_agreement_activates_once_and_the_code_becomes_used()
    {
        var agreement = Pending(Now);

        var active = agreement.Activate(Now.AddMinutes(1));

        Assert.Equal(AgreementStatus.Active, active.Status);
        Assert.Equal(Now.AddMinutes(1), active.ActivatedAt);
        Assert.Equal(Now.AddMinutes(1), active.Code!.UsedAt);
        Assert.Equal(new AgreementHistoryEntry(AgreementStatus.Active, Now.AddMinutes(1), AgreementActors.Student), active.History[^1]);
        Assert.Equal("code_used", Assert.Throws<GoneException>(() => active.Activate(Now.AddMinutes(2))).Reason);
        Assert.Equal("agreement_state", Assert.Throws<ConflictException>(() => active.Cancel(Now)).Reason);
    }

    [Fact]
    public void Code_checks_expired_blocked_and_missing()
    {
        Assert.Equal("code_expired", Assert.Throws<GoneException>(() => Pending(Now).EnsureCodeUsable(Now.AddMinutes(10))).Reason);
        var blocked = Pending(Now) with { Code = Pending(Now).Code! with { FailedAttempts = ClosingCode.MaximumFailedAttempts } };
        Assert.Equal("code_invalid", Assert.Throws<NotFoundException>(() => blocked.EnsureCodeUsable(Now)).Code);
        var cancelled = Pending(Now).Cancel(Now);
        Assert.Null(cancelled.Code);
        Assert.Equal("code_invalid", Assert.Throws<NotFoundException>(() => cancelled.EnsureCodeUsable(Now)).Code);
        Assert.Equal("code_invalid", Assert.Throws<NotFoundException>(() => Pending(Now).Activate(Now).End(null, null, Now).EnsureCodeUsable(Now)).Code);
    }

    [Fact]
    public void Only_active_rentals_end_and_rooms_move_between_available_and_rented()
    {
        var ended = Pending(Now).Activate(Now).End("contract_finished", "Gracias", Now.AddDays(30));

        Assert.Equal(AgreementStatus.Ended, ended.Status);
        Assert.Equal("contract_finished", ended.EndReason);
        Assert.Equal(AgreementActors.Landlord, ended.History[^1].Actor);
        Assert.False(ended.IsOpen);
        Assert.Equal("agreement_state", Assert.Throws<ConflictException>(() => Pending(Now).End(null, null, Now)).Reason);

        var room = new Room { Status = RoomStatus.Available };
        Assert.Equal(RoomStatus.Rented, room.Rent(Now).Status);
        Assert.Equal(RoomStatus.Available, room.Rent(Now).Release(Now).Status);
        var paused = room with { Status = RoomStatus.Paused };
        Assert.Equal("room_unavailable", Assert.Throws<ConflictException>(() => paused.Rent(Now)).Reason);
        Assert.Equal("room_state", Assert.Throws<ConflictException>(() => room.Release(Now)).Reason);
    }

    // --- Código y términos -------------------------------------------

    [Fact]
    public void Closing_codes_are_random_hashed_and_tolerant_to_format()
    {
        var (code, token) = ClosingCodes.Generate();
        var (other, _) = ClosingCodes.Generate();

        Assert.Equal(8, code.Length);
        Assert.All(code, character => Assert.Contains(character, ClosingCodes.Alphabet));
        Assert.Equal(43, token.Length);
        Assert.NotEqual(code, other);
        Assert.Equal(code, ClosingCodes.NormalizeCode($"{code[..4].ToLowerInvariant()}-{code[4..]}"));
        Assert.Equal(64, ClosingCodes.Hash(code).Length);
        Assert.NotEqual(code, ClosingCodes.Hash(code));
        Assert.Throws<RequestValidationException>(() => ClosingCodes.NormalizeCode("ABCD-EFG"));
        Assert.Throws<RequestValidationException>(() => ClosingCodes.NormalizeCode("ABCD0FGH"));
        Assert.Throws<RequestValidationException>(() => ClosingCodes.NormalizeQrToken("corto"));
    }

    [Theory]
    [InlineData(499, 0, "2026-12-01", 6, 5, "monthlyRent")]
    [InlineData(3000, 6001, "2026-12-01", 6, 5, "deposit")]
    [InlineData(3000, -1, "2026-12-01", 6, 5, "deposit")]
    [InlineData(3000, 0, "2026-10-12", 6, 5, "startDate")]
    [InlineData(3000, 0, "2027-02-11", 6, 5, "startDate")]
    [InlineData(3000, 0, "01/12/2026", 6, 5, "startDate")]
    [InlineData(3000, 0, "2026-12-01", 0, 5, "durationMonths")]
    [InlineData(3000, 0, "2026-12-01", 25, 5, "durationMonths")]
    [InlineData(3000, 0, "2026-12-01", 6, 29, "paymentDay")]
    [InlineData(3000, 0, "2026-12-01", 6, 0, "paymentDay")]
    public void Terms_are_validated(int rent, int deposit, string start, int months, int day, string field)
    {
        var body = new CreateAgreementRequest { MonthlyRent = rent, Deposit = deposit, StartDate = start, DurationMonths = months, PaymentDay = day };

        var exception = Assert.Throws<RequestValidationException>(() => AgreementRules.Terms(body, AgreementRules.Today(Now)));

        Assert.Equal(field, Assert.Single(exception.Errors.Keys));
    }

    [Fact]
    public void Terms_compute_the_end_date_and_the_next_payment()
    {
        var terms = AgreementRules.Terms(
            new CreateAgreementRequest { MonthlyRent = 3000, Deposit = 3000, StartDate = "2026-12-01", DurationMonths = 4, PaymentDay = 5 },
            AgreementRules.Today(Now));

        Assert.Equal(new DateOnly(2027, 4, 1), terms.EndDate);
        Assert.Equal(new DateOnly(2026, 12, 5), AgreementRules.NextPaymentDate(terms, new DateOnly(2026, 11, 12)));
        Assert.Equal(new DateOnly(2027, 1, 5), AgreementRules.NextPaymentDate(terms, new DateOnly(2026, 12, 6)));
        Assert.Equal(new DateOnly(2027, 1, 5), AgreementRules.NextPaymentDate(terms, new DateOnly(2027, 1, 5)));
        Assert.Null(AgreementRules.NextPaymentDate(terms, new DateOnly(2027, 3, 6)));
    }

    // --- Crear -------------------------------------------------------

    [Fact]
    public async Task Landlord_creates_from_an_accepted_request_and_gets_a_code()
    {
        var world = World();

        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);

        Assert.Equal("pending", created.Status);
        Assert.NotNull(created.ClosingCode);
        Assert.Equal(Now + ClosingCode.Lifetime, created.ClosingCode!.ExpiresAt);
        Assert.Equal("Ana", created.Student.Name);
        Assert.Equal("Casa Azul · Cuarto 101", created.Room.Title);
        Assert.Equal(new DateOnly(2027, 6, 1), created.Terms.EndDate);
        var stored = world.Agreements.Agreements[created.Id];
        Assert.Equal(Student, stored.StudentId);
        Assert.Equal(ClosingCodes.Hash(created.ClosingCode.BackupCode), stored.Code!.CodeHash);
        Assert.DoesNotContain(created.ClosingCode.BackupCode, stored.Code.CodeHash);

        var detail = await world.Service.GetAsync(Owner, created.Id, CancellationToken.None);
        Assert.Null(detail.ClosingCode);
    }

    [Fact]
    public async Task Create_rules_and_codes()
    {
        var world = World();
        await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.CreateAsync(Other, Body(world.RequestId), CancellationToken.None));
        await world.Landlords.CreateAsync(Landlord(Other), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.CreateAsync(Other, Body(world.RequestId), CancellationToken.None));

        var pending = await Request(world, Other, RentalRequestStatus.Pending);
        Assert.Equal("request_state", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.CreateAsync(Owner, Body(pending), CancellationToken.None))).Reason);

        await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        var second = await Request(world, Other, RentalRequestStatus.Accepted, studentOverride: "665f1c2a9b1e4a0000000d0d");
        Assert.Equal("agreement_open", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.CreateAsync(Owner, Body(second), CancellationToken.None))).Reason);

        var room = world.Rooms.Rooms[world.RoomId];
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };
        Assert.Equal("room_unavailable", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.CreateAsync(Owner, Body(second), CancellationToken.None))).Reason);
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.CreateAsync(Owner, Body("no-es-id"), CancellationToken.None));
    }

    [Fact]
    public async Task New_code_replaces_the_previous_one_and_cancel_invalidates_it()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);

        var reissued = await world.Service.IssueCodeAsync(Owner, created.Id, CancellationToken.None);

        Assert.NotEqual(created.ClosingCode!.BackupCode, reissued.ClosingCode!.BackupCode);
        Assert.Equal("code_invalid", (await Assert.ThrowsAsync<NotFoundException>(
            () => world.Service.PreviewAsync(Student, new ClosingCodeRequest { Code = created.ClosingCode.BackupCode }, CancellationToken.None))).Code);
        var preview = await world.Service.PreviewAsync(Student, new ClosingCodeRequest { QrToken = reissued.ClosingCode.QrToken }, CancellationToken.None);
        Assert.Equal(created.Id, preview.Id);
        Assert.Equal("Bob", preview.Landlord.Name);
        Assert.Equal("Sin mascotas", preview.Building.Rules);

        var cancelled = await world.Service.CancelAsync(Owner, created.Id, CancellationToken.None);
        Assert.Equal("cancelled", cancelled.Status);
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Service.PreviewAsync(Student, new ClosingCodeRequest { Code = reissued.ClosingCode.BackupCode }, CancellationToken.None));
        Assert.Equal("agreement_state", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.IssueCodeAsync(Owner, created.Id, CancellationToken.None))).Reason);
    }

    // --- Aceptar -----------------------------------------------------

    [Fact]
    public async Task Accept_rents_the_room_and_closes_other_requests_and_visits()
    {
        var world = World();
        var otherRequest = await Request(world, Other, RentalRequestStatus.Pending);
        var otherVisit = await world.Visits.CreateAsync(Visit(Other, world.RoomId), CancellationToken.None);
        var ownVisit = await world.Visits.CreateAsync(Visit(Student, world.RoomId), CancellationToken.None);
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);

        var (rental, effectsPending) = await world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None);

        Assert.False(effectsPending);
        Assert.Equal(created.Id, rental.AgreementId);
        Assert.Equal("Calle Privada", rental.Address.Street);
        Assert.Equal(20.0512, rental.Location.Lat);
        Assert.Equal(world.ConversationId, rental.ConversationId);
        Assert.Equal(new RentalNextPayment(new DateOnly(2026, 12, 5), 3000), rental.NextPayment);
        Assert.Equal(RoomStatus.Rented, world.Rooms.Rooms[world.RoomId].Status);
        Assert.Equal(AgreementStatus.Active, world.Agreements.Agreements[created.Id].Status);
        var rejected = world.Requests.Requests[otherRequest];
        Assert.Equal(RentalRequestStatus.Rejected, rejected.Status);
        Assert.Equal("not_available", rejected.Rejection!.Reason);
        Assert.Equal(RentalRequestStatus.Accepted, world.Requests.Requests[world.RequestId].Status);
        Assert.Equal(VisitStatus.Cancelled, world.Visits.Visits[otherVisit.Id].Status);
        Assert.Equal(VisitActors.Landlord, world.Visits.Visits[otherVisit.Id].CancelledBy);
        Assert.Equal(VisitStatus.Requested, world.Visits.Visits[ownVisit.Id].Status);

        Assert.Equal("code_used", (await Assert.ThrowsAsync<GoneException>(
            () => world.Service.AcceptAsync(Student, Accept(created.ClosingCode.BackupCode), CancellationToken.None))).Reason);
        Assert.Equal(created.Id, (await world.Service.GetMineAsync(Student, CancellationToken.None)).AgreementId);
    }

    [Fact]
    public async Task Accept_rules_terms_expiry_and_request_state()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.AcceptAsync(Student, new AcceptAgreementRequest { Code = created.ClosingCode!.BackupCode }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.AcceptAsync(Student, new AcceptAgreementRequest { Code = created.ClosingCode!.BackupCode, QrToken = created.ClosingCode.QrToken, AcceptedTerms = true }, CancellationToken.None));

        world.Requests.Requests[world.RequestId] = world.Requests.Requests[world.RequestId] with { Status = RentalRequestStatus.Cancelled };
        Assert.Equal("request_state", (await Assert.ThrowsAsync<ConflictException>(
            () => world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None))).Reason);

        world.Clock.Now = Now.AddMinutes(11);
        Assert.Equal("code_expired", (await Assert.ThrowsAsync<GoneException>(
            () => world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None))).Reason);
        Assert.Equal(RoomStatus.Available, world.Rooms.Rooms[world.RoomId].Status);
    }

    [Fact]
    public async Task Another_account_gets_404_and_five_failures_block_the_code()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        var code = created.ClosingCode!.BackupCode;

        for (var attempt = 0; attempt < ClosingCode.MaximumFailedAttempts; attempt++)
        {
            Assert.Equal("code_invalid", (await Assert.ThrowsAsync<NotFoundException>(
                () => world.Service.PreviewAsync(Other, new ClosingCodeRequest { Code = code }, CancellationToken.None))).Code);
        }

        Assert.Equal(ClosingCode.MaximumFailedAttempts, world.Agreements.Agreements[created.Id].Code!.FailedAttempts);
        Assert.Equal("code_invalid", (await Assert.ThrowsAsync<NotFoundException>(
            () => world.Service.AcceptAsync(Student, Accept(code), CancellationToken.None))).Code);
        Assert.Equal(RoomStatus.Available, world.Rooms.Rooms[world.RoomId].Status);
    }

    [Fact]
    public async Task Room_changed_midway_reverts_the_agreement_without_a_code()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        var service = world.With(new FailingRoomReplace(world.Rooms));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None));

        Assert.Equal("room_unavailable", exception.Reason);
        var stored = world.Agreements.Agreements[created.Id];
        Assert.Equal(AgreementStatus.Pending, stored.Status);
        Assert.Null(stored.Code);
        Assert.Null(stored.ActivatedAt);
    }

    [Fact]
    public async Task Two_simultaneous_accepts_rent_the_room_once()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        world.Lock.HoldDelay = TimeSpan.FromMilliseconds(50);

        var first = world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None);
        var second = world.Service.AcceptAsync(Student, Accept(created.ClosingCode.BackupCode), CancellationToken.None);
        var results = await Task.WhenAll(Capture(first), Capture(second));

        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is ConflictException or GoneException);
        Assert.Equal(2, world.Rooms.Rooms[world.RoomId].Version);
    }

    [Fact]
    public async Task Effects_failures_do_not_undo_the_rental()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        world.Visits.Unavailable = true;

        var (rental, effectsPending) = await world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None);

        Assert.True(effectsPending);
        Assert.Equal(created.Id, rental.AgreementId);
        Assert.Equal(RoomStatus.Rented, world.Rooms.Rooms[world.RoomId].Status);
    }

    // --- Mi renta y fin de renta ------------------------------------

    [Fact]
    public async Task End_frees_the_room_and_removes_the_address()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);
        await world.Service.AcceptAsync(Student, Accept(created.ClosingCode!.BackupCode), CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Service.EndAsync(Owner, created.Id, new EndAgreementRequest { Reason = "otro" }, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.EndAsync(Other, created.Id, null, CancellationToken.None));
        await world.Landlords.CreateAsync(Landlord(Other), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.EndAsync(Other, created.Id, null, CancellationToken.None));
        var ended = await world.Service.EndAsync(Owner, created.Id, new EndAgreementRequest { Reason = "early_termination" }, CancellationToken.None);

        Assert.Equal("ended", ended.Status);
        Assert.Equal(RoomStatus.Available, world.Rooms.Rooms[world.RoomId].Status);
        Assert.Equal("no_active_rental", (await Assert.ThrowsAsync<NotFoundException>(() => world.Service.GetMineAsync(Student, CancellationToken.None))).Code);
        Assert.True(await new RentalHistoryQuery(world.Agreements).HasRentedAsync(Student, world.RoomId, CancellationToken.None));
        Assert.False(await new RentalHistoryQuery(world.Agreements).HasRentedAsync(Other, world.RoomId, CancellationToken.None));
        Assert.Equal("agreement_state", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.EndAsync(Owner, created.Id, null, CancellationToken.None))).Reason);
    }

    [Fact]
    public async Task Landlord_list_filters_by_status_and_room()
    {
        var world = World();
        var created = await world.Service.CreateAsync(Owner, Body(world.RequestId), CancellationToken.None);

        var pending = await world.Service.ListAsync(Owner, "pending", world.RoomId, 1, 20, CancellationToken.None);
        var active = await world.Service.ListAsync(Owner, "active", null, 1, 20, CancellationToken.None);

        Assert.Equal(created.Id, Assert.Single(pending.Items).Id);
        Assert.Null(pending.Items[0].ClosingCode);
        Assert.Empty(active.Items);
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.ListAsync(Owner, "rented", null, 1, 20, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.ListAsync(Owner, null, "x", 1, 20, CancellationToken.None));
    }

    // --- Apoyo -------------------------------------------------------

    private static async Task<Exception?> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Agreement Pending(DateTimeOffset now) =>
        new()
        {
            Id = "6a0000000000000000000001",
            RoomId = "6a0000000000000000000002",
            Status = AgreementStatus.Pending,
            Terms = new AgreementTerms(3000, 0, new DateOnly(2026, 12, 1), 6, new DateOnly(2027, 6, 1), 5),
            Code = new ClosingCode("hash", "qr", now + ClosingCode.Lifetime, null, 0),
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static CreateAgreementRequest Body(string requestId) =>
        new() { RequestId = requestId, MonthlyRent = 3000, Deposit = 3000, StartDate = "2026-12-01", DurationMonths = 6, PaymentDay = 5 };

    private static AcceptAgreementRequest Accept(string code) => new() { Code = code, AcceptedTerms = true };

    private static Visit Visit(string userId, string roomId) =>
        new()
        {
            UserId = userId,
            RoomId = roomId,
            ScheduledAt = Now.AddDays(3),
            Status = VisitStatus.Requested,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static LandlordProfile Landlord(string userId) =>
        new()
        {
            UserId = userId,
            FullName = userId == Owner ? "Bob Pérez" : "Carla Ruiz",
            Phone = "+527711234567",
            RentalMode = "individual",
            AcceptedTermsVersion = "1.0",
            AcceptedTermsAt = Now,
            CreatedAt = Now,
        };

    private static async Task<string> Request(TestWorld world, string studentId, RentalRequestStatus status, string? studentOverride = null)
    {
        var student = studentOverride ?? studentId;
        var conversation = await world.Conversations.CreateAsync(
            new Conversation { RoomId = world.RoomId, StudentId = student, OwnerId = Owner, CreatedAt = Now, LastMessageAt = Now },
            CancellationToken.None);
        var request = await world.Requests.CreateAsync(
            new RentalRequest
            {
                RoomId = world.RoomId,
                BuildingId = world.BuildingId,
                StudentId = student,
                OwnerId = Owner,
                Status = status,
                ConversationId = conversation.Id,
                CreatedAt = Now,
                UpdatedAt = Now,
            },
            CancellationToken.None);
        return request!.Id;
    }

    private sealed record TestWorld(
        AgreementService Service,
        InMemoryAgreementRepository Agreements,
        InMemoryRentalRequestRepository Requests,
        InMemoryConversationRepository Conversations,
        InMemoryRoomRepository Rooms,
        InMemoryBuildingRepository Buildings,
        InMemoryLandlordProfileRepository Landlords,
        InMemoryProfileRepository Profiles,
        InMemoryVisitRepository Visits,
        InMemoryUserLock Lock,
        ManualTimeProvider Clock,
        string BuildingId,
        string RoomId)
    {
        public string RequestId { get; set; } = string.Empty;

        public string ConversationId { get; set; } = string.Empty;

        public AgreementService With(IRoomRepository rooms) =>
            new(Agreements, Requests, Conversations, rooms, Buildings, Landlords, Profiles, new UniversityStub(), Visits, Lock, Clock);
    }

    private static TestWorld World()
    {
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        var landlords = new InMemoryLandlordProfileRepository();
        var profiles = new InMemoryProfileRepository();
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
                Rules = "Sin mascotas",
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None).GetAwaiter().GetResult();
        var room = rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None).GetAwaiter().GetResult()[0];

        var agreements = new InMemoryAgreementRepository();
        var requests = new InMemoryRentalRequestRepository();
        var conversations = new InMemoryConversationRepository();
        var visits = new InMemoryVisitRepository();
        var userLock = new InMemoryUserLock();
        var clock = new ManualTimeProvider(Now);
        var service = new AgreementService(agreements, requests, conversations, rooms, buildings, landlords, profiles, new UniversityStub(), visits, userLock, clock);
        var world = new TestWorld(service, agreements, requests, conversations, rooms, buildings, landlords, profiles, visits, userLock, clock, building.Id, room.Id);
        world.RequestId = Request(world, Student, RentalRequestStatus.Accepted).GetAwaiter().GetResult();
        world.ConversationId = requests.Requests[world.RequestId].ConversationId;
        return world;
    }

    // Simula que otro cambio del cuarto gana justo antes de marcarlo rentado.
    private sealed class FailingRoomReplace(InMemoryRoomRepository inner) : IRoomRepository
    {
        public Task<IReadOnlyList<Room>> CreateManyAsync(IReadOnlyList<Room> rooms, CancellationToken cancellationToken) => inner.CreateManyAsync(rooms, cancellationToken);

        public Task<Room?> GetAsync(string id, CancellationToken cancellationToken) => inner.GetAsync(id, cancellationToken);

        public Task<IReadOnlyList<Room>> GetManyAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken) => inner.GetManyAsync(ids, cancellationToken);

        public Task<(IReadOnlyList<Room> Items, long TotalItems)> ListByBuildingAsync(string buildingId, int page, int pageSize, CancellationToken cancellationToken) =>
            inner.ListByBuildingAsync(buildingId, page, pageSize, cancellationToken);

        public Task<IReadOnlyList<Room>> ListAllByBuildingAsync(string buildingId, CancellationToken cancellationToken) => inner.ListAllByBuildingAsync(buildingId, cancellationToken);

        public Task<IReadOnlyList<Room>> ListAllByOwnerAsync(string ownerId, CancellationToken cancellationToken) => inner.ListAllByOwnerAsync(ownerId, cancellationToken);

        public Task<bool> ReplaceAsync(Room room, long expectedVersion, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> DeleteAsync(string id, long expectedVersion, CancellationToken cancellationToken) => inner.DeleteAsync(id, expectedVersion, cancellationToken);

        public Task DeleteByBuildingAsync(string buildingId, CancellationToken cancellationToken) => inner.DeleteByBuildingAsync(buildingId, cancellationToken);

        public Task<IReadOnlyList<Room>> ListAvailableByBuildingsAsync(IReadOnlyCollection<string> buildingIds, RoomFilter filter, CancellationToken cancellationToken) =>
            inner.ListAvailableByBuildingsAsync(buildingIds, filter, cancellationToken);

        public Task SetStatusByBuildingAsync(string buildingId, RoomStatus from, RoomStatus to, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.SetStatusByBuildingAsync(buildingId, from, to, now, cancellationToken);
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
