using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-16: reglas de solicitudes y chat con dobles en memoria y reloj fijo.
public sealed class MessagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);

    private const string Student = "665f1c2a9b1e4a00000a11ce";
    private const string Owner = "665f1c2a9b1e4a0000000b0b";
    private const string Stranger = "665f1c2a9b1e4a0000000ca1";

    // --- Reglas ---------------------------------------------------

    [Fact]
    public void Ids_have_object_id_format_and_start_with_the_timestamp()
    {
        var id = MessagingIds.New(Now);

        Assert.Matches("^[0-9a-f]{24}$", id);
        Assert.StartsWith(Now.ToUnixTimeSeconds().ToString("x8", System.Globalization.CultureInfo.InvariantCulture), id);
        Assert.NotEqual(id, MessagingIds.New(Now));
    }

    [Theory]
    [InlineData("María José López", "María")]
    [InlineData("  Ana  ", "Ana")]
    [InlineData("", "Arrendador")]
    [InlineData(null, "Arrendador")]
    public void Landlord_name_is_only_the_first_name(string? full, string expected) =>
        Assert.Equal(expected, MessagingRules.FirstName(full));

    [Theory]
    [InlineData("pending", RentalRequestStatus.Pending)]
    [InlineData("cancelled", RentalRequestStatus.Cancelled)]
    public void Status_filter_accepts_the_catalog(string value, RentalRequestStatus expected) =>
        Assert.Equal(expected, MessagingRules.ParseStatus(value));

    [Fact]
    public void Status_filter_rejects_unknown_values() =>
        Assert.Throws<RequestValidationException>(() => MessagingRules.ParseStatus("closed"));

    // --- Crear solicitud -------------------------------------------

    [Fact]
    public async Task Create_builds_request_and_linked_conversation()
    {
        var world = World();

        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit", "visit_possible"], " Hola "), CancellationToken.None);

        var stored = Assert.Single(world.RequestStore.Requests.Values);
        var conversation = Assert.Single(world.ConversationStore.Conversations.Values);
        Assert.Equal("pending", created.Status);
        Assert.Equal("Hola", created.Message);
        Assert.Equal(conversation.Id, stored.ConversationId);
        Assert.Equal(stored.Id, conversation.RequestId);
        Assert.Equal(Owner, conversation.OwnerId);
        Assert.Null(created.Student);
        Assert.Equal("Laura", created.Landlord.Name);
        Assert.Equal("Casa Azul · Cuarto 101", created.Room.Title);
        Assert.True(created.RoomAvailable);
    }

    [Theory]
    [InlineData(new[] { "deposit", "deposit" }, "x")]
    [InlineData(new[] { "price" }, "x")]
    [InlineData(new[] { "available_now", "services_included", "deposit", "minimum_contract", "visit_possible", "pets_allowed" }, "x")]
    [InlineData(new string[0], "   ")]
    public async Task Create_validates_questions_and_message(string[] questions, string message)
    {
        var world = World();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body(questions, message), CancellationToken.None));
        Assert.Empty(world.RequestStore.Requests);
    }

    [Fact]
    public async Task Create_rejects_long_message_and_bad_room_id()
    {
        var world = World();

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body([], new string('a', 501)), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.CreateAsync(Student, "room-1", Body(["deposit"], null), CancellationToken.None));
    }

    [Fact]
    public async Task Create_requires_a_published_room_and_not_an_own_room()
    {
        var world = World();
        var room = world.Rooms.Rooms[world.RoomId];

        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Requests.CreateAsync(Student, "ffffffffffffffffffffffff", Body(["deposit"], null), CancellationToken.None));
        var own = await Assert.ThrowsAsync<BusinessRuleException>(
            () => world.Requests.CreateAsync(Owner, world.RoomId, Body(["deposit"], null), CancellationToken.None));
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Paused };
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None));

        Assert.Equal("own_room", own.Rule);
        Assert.Empty(world.RequestStore.Requests);
    }

    [Fact]
    public async Task Only_one_active_request_per_room_and_ten_in_total()
    {
        var world = World(rooms: 11);
        await world.Requests.CreateAsync(Student, world.RoomIds[0], Body(["deposit"], null), CancellationToken.None);

        var duplicate = await Assert.ThrowsAsync<ConflictException>(
            () => world.Requests.CreateAsync(Student, world.RoomIds[0], Body(["deposit"], null), CancellationToken.None));
        for (var index = 1; index < 10; index++)
        {
            await world.Requests.CreateAsync(Student, world.RoomIds[index], Body(["deposit"], null), CancellationToken.None);
        }

        var limit = await Assert.ThrowsAsync<ConflictException>(
            () => world.Requests.CreateAsync(Student, world.RoomIds[10], Body(["deposit"], null), CancellationToken.None));

        Assert.Equal("active_request_exists", duplicate.Reason);
        Assert.Equal("active_requests_limit", limit.Reason);
        Assert.Equal(10, world.ConversationStore.Conversations.Count);
    }

    [Fact]
    public async Task Suspended_participants_cannot_start_a_request()
    {
        var world = World();
        await world.Moderation.CreateAsync(new UserSuspension(Owner, "fraude", "admin", Now), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None));

        Assert.Equal("participant_suspended", exception.Reason);
    }

    [Fact]
    public async Task Create_fails_when_the_user_lock_is_held()
    {
        var world = World();
        await using var held = await world.Lock.TryAcquireAsync(RateLimitKeys.MessagingLock(Student), TimeSpan.FromSeconds(5), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None));

        Assert.Equal("request_in_progress", exception.Reason);
    }

    [Fact]
    public async Task A_failed_conversation_leaves_no_request_behind()
    {
        var world = World();
        world.ConversationStore.FailCreate = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None));

        Assert.Empty(world.RequestStore.Requests);
    }

    // --- Transiciones ----------------------------------------------

    [Fact]
    public async Task Landlord_accepts_once_and_sees_the_student_summary_and_visit()
    {
        var world = World();
        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);
        await world.Visits.CreateAsync(
            new Visit
            {
                UserId = Student,
                RoomId = world.RoomId,
                ScheduledAt = Now.AddDays(2),
                Status = VisitStatus.Requested,
                CreatedAt = Now,
                UpdatedAt = Now,
                History = [new VisitHistoryEntry(VisitStatus.Requested, Now, "student")],
            },
            CancellationToken.None);

        var accepted = await world.Requests.AcceptAsync(Owner, created.Id, CancellationToken.None);
        var again = await Assert.ThrowsAsync<ConflictException>(() => world.Requests.AcceptAsync(Owner, created.Id, CancellationToken.None));

        Assert.Equal("accepted", accepted.Status);
        Assert.Equal(new RequestStudentSummary("Ana", "Universidad Tecnológica de Tula-Tepeji"), accepted.Student);
        Assert.Equal("requested", accepted.Visit?.Status);
        Assert.Equal("request_state", again.Reason);
    }

    [Fact]
    public async Task Reject_requires_a_valid_reason_and_closes_the_conversation()
    {
        var world = World();
        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.RejectAsync(Owner, created.Id, new RejectRentalRequest { Reason = "spam" }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.RejectAsync(Owner, created.Id, new RejectRentalRequest { Reason = "other" }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Requests.RejectAsync(Owner, created.Id, new RejectRentalRequest { Reason = "not_available", Note = new string('n', 301) }, CancellationToken.None));
        var rejected = await world.Requests.RejectAsync(Owner, created.Id, new RejectRentalRequest { Reason = "not_available", Note = " Ya se rentó " }, CancellationToken.None);

        Assert.Equal("rejected", rejected.Status);
        Assert.Equal(new RequestRejectionResponse("not_available", "Ya se rentó"), rejected.Rejection);
        Assert.Equal(ConversationStatus.Closed, world.ConversationStore.Conversations[created.ConversationId].Status);
        Assert.Null(rejected.Visit);
    }

    [Fact]
    public async Task Student_cancels_pending_or_accepted_but_not_twice()
    {
        var world = World();
        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);
        await world.Requests.AcceptAsync(Owner, created.Id, CancellationToken.None);

        var cancelled = await world.Requests.CancelAsync(Student, created.Id, CancellationToken.None);
        var again = await Assert.ThrowsAsync<ConflictException>(() => world.Requests.CancelAsync(Student, created.Id, CancellationToken.None));
        var renewed = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal("request_state", again.Reason);
        Assert.Equal("pending", renewed.Status);
    }

    [Fact]
    public async Task Accept_fails_when_the_room_is_no_longer_published()
    {
        var world = World();
        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);
        var room = world.Rooms.Rooms[world.RoomId];
        world.Rooms.Rooms[room.Id] = room with { Status = RoomStatus.Rented };

        var exception = await Assert.ThrowsAsync<ConflictException>(() => world.Requests.AcceptAsync(Owner, created.Id, CancellationToken.None));
        var mine = await world.Requests.GetMineAsync(Student, created.Id, CancellationToken.None);

        Assert.Equal("room_unavailable", exception.Reason);
        Assert.False(mine.RoomAvailable);
    }

    [Fact]
    public async Task Requests_are_private_to_their_student_and_owner()
    {
        var world = World();
        var created = await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None);
        await world.Landlords.CreateAsync(Landlord(Stranger, "Pedro Ruiz"), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => world.Requests.GetMineAsync(Stranger, created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => world.Requests.GetReceivedAsync(Stranger, created.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => world.Requests.CancelAsync(Owner, created.Id, CancellationToken.None));
        Assert.Empty((await world.Requests.ListReceivedAsync(Stranger, null, null, 1, 20, CancellationToken.None)).Items);
    }

    [Fact]
    public async Task Landlord_inbox_requires_landlord_mode_and_filters_by_status_and_room()
    {
        var world = World(rooms: 2);
        var first = await world.Requests.CreateAsync(Student, world.RoomIds[0], Body(["deposit"], null), CancellationToken.None);
        await world.Requests.CreateAsync(Student, world.RoomIds[1], Body(["deposit"], null), CancellationToken.None);
        await world.Requests.AcceptAsync(Owner, first.Id, CancellationToken.None);

        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(
            () => world.Requests.ListReceivedAsync(Student, null, null, 1, 20, CancellationToken.None));
        var accepted = await world.Requests.ListReceivedAsync(Owner, "accepted", null, 1, 20, CancellationToken.None);
        var byRoom = await world.Requests.ListReceivedAsync(Owner, null, world.RoomIds[1], 1, 20, CancellationToken.None);
        var mine = await world.Requests.ListMineAsync(Student, null, 1, 1, CancellationToken.None);

        Assert.Equal("landlord_required", forbidden.Reason);
        Assert.Equal(first.Id, Assert.Single(accepted.Items).Id);
        Assert.Equal(world.RoomIds[1], Assert.Single(byRoom.Items).Room.Id);
        Assert.Equal(2, mine.TotalItems);
        Assert.Equal(2, mine.TotalPages);
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Requests.ListMineAsync(Student, null, 0, 20, CancellationToken.None));
    }

    // --- Chat ------------------------------------------------------

    [Fact]
    public async Task Messages_are_idempotent_ordered_and_paged_by_cursor()
    {
        var world = World();
        var conversation = await OpenConversationAsync(world);

        var (first, created) = await world.Chat.SendAsync(Student, conversation, Message("Hola", "8f1d2c3a-0000-4000-8000-000000000001"), CancellationToken.None);
        var (repeat, createdAgain) = await world.Chat.SendAsync(Student, conversation, Message("Hola", "8f1d2c3a-0000-4000-8000-000000000001"), CancellationToken.None);
        var (reply, _) = await world.Chat.SendAsync(Owner, conversation, Message("Buen día", "8f1d2c3a-0000-4000-8000-000000000002"), CancellationToken.None);

        var (loaded, etag) = await world.Chat.GetForParticipantAsync(Student, conversation, CancellationToken.None);
        var page = await world.Chat.ListMessagesAsync(Student, loaded, null, 1, CancellationToken.None);
        var rest = await world.Chat.ListMessagesAsync(Student, loaded, first.Id, 50, CancellationToken.None);

        Assert.True(created);
        Assert.False(createdAgain);
        Assert.Equal(first.Id, repeat.Id);
        Assert.Equal(2, world.ConversationStore.Messages.Count);
        Assert.Equal($"\"m-{reply.Id}\"", etag);
        Assert.True(page.HasMore);
        Assert.True(Assert.Single(page.Items).Mine);
        Assert.Equal("landlord", Assert.Single(rest.Items).SenderRole);
        Assert.False(rest.Items[0].Mine);
        Assert.Equal(10, rest.PollAfterSeconds);
    }

    [Theory]
    [InlineData("", "8f1d2c3a-0000-4000-8000-000000000001")]
    [InlineData("   ", "8f1d2c3a-0000-4000-8000-000000000001")]
    [InlineData("hola", "no-es-uuid")]
    [InlineData("hola", null)]
    public async Task Send_validates_text_and_client_id(string text, string? clientId)
    {
        var world = World();
        var conversation = await OpenConversationAsync(world);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Chat.SendAsync(Student, conversation, Message(text, clientId), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => world.Chat.SendAsync(Student, conversation, Message(new string('x', 1001), "8f1d2c3a-0000-4000-8000-000000000003"), CancellationToken.None));
    }

    [Fact]
    public async Task Closed_conversations_and_suspended_participants_cannot_write()
    {
        var world = World(rooms: 2);
        var open = await OpenConversationAsync(world);
        var request = await world.Requests.CreateAsync(Student, world.RoomIds[1], Body(["deposit"], null), CancellationToken.None);
        await world.Requests.CancelAsync(Student, request.Id, CancellationToken.None);

        var closed = await Assert.ThrowsAsync<ConflictException>(
            () => world.Chat.SendAsync(Student, request.ConversationId, Message("hola", "8f1d2c3a-0000-4000-8000-000000000004"), CancellationToken.None));
        await world.Moderation.CreateAsync(new UserSuspension(Student, "spam", "admin", Now), CancellationToken.None);
        var suspended = await Assert.ThrowsAsync<ConflictException>(
            () => world.Chat.SendAsync(Owner, open, Message("hola", "8f1d2c3a-0000-4000-8000-000000000005"), CancellationToken.None));

        Assert.Equal("conversation_closed", closed.Reason);
        Assert.Equal("participant_suspended", suspended.Reason);
    }

    [Fact]
    public async Task Unread_counts_only_the_other_participant_and_read_marks_progress()
    {
        var world = World();
        var conversation = await OpenConversationAsync(world);
        await world.Chat.SendAsync(Student, conversation, Message("uno", "8f1d2c3a-0000-4000-8000-000000000006"), CancellationToken.None);
        var (second, _) = await world.Chat.SendAsync(Student, conversation, Message("dos", "8f1d2c3a-0000-4000-8000-000000000007"), CancellationToken.None);

        var before = Assert.Single((await world.Chat.ListAsync(Owner, 1, 20, CancellationToken.None)).Items);
        var studentView = Assert.Single((await world.Chat.ListAsync(Student, 1, 20, CancellationToken.None)).Items);
        await world.Chat.MarkReadAsync(Owner, conversation, new MarkReadRequest { LastMessageId = second.Id }, CancellationToken.None);
        var after = Assert.Single((await world.Chat.ListAsync(Owner, 1, 20, CancellationToken.None)).Items);

        Assert.Equal(2, before.UnreadCount);
        Assert.Equal(new ConversationCounterpart("Ana", "student"), before.Counterpart);
        Assert.Equal(0, studentView.UnreadCount);
        Assert.Equal(new ConversationCounterpart("Laura", "landlord"), studentView.Counterpart);
        Assert.Equal(0, after.UnreadCount);
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Chat.MarkReadAsync(Owner, conversation, new MarkReadRequest { LastMessageId = "ffffffffffffffffffffffff" }, CancellationToken.None));
    }

    [Fact]
    public async Task Conversations_are_private_and_list_limits_are_validated()
    {
        var world = World();
        var conversation = await OpenConversationAsync(world);
        var (loaded, _) = await world.Chat.GetForParticipantAsync(Student, conversation, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => world.Chat.GetForParticipantAsync(Stranger, conversation, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => world.Chat.SendAsync(Stranger, conversation, Message("hola", "8f1d2c3a-0000-4000-8000-000000000008"), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Chat.ListMessagesAsync(Student, loaded, null, 51, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Chat.ListMessagesAsync(Student, loaded, "abc", 10, CancellationToken.None));
        Assert.Empty((await world.Chat.ListAsync(Stranger, 1, 20, CancellationToken.None)).Items);
    }

    // --- Soportes --------------------------------------------------

    private static CreateRentalRequest Body(string[] questions, string? message) =>
        new() { QuickQuestions = questions, Message = message };

    private static SendMessageRequest Message(string text, string? clientId) =>
        new() { Text = text, ClientMessageId = clientId };

    private static LandlordProfile Landlord(string userId, string name) =>
        new()
        {
            UserId = userId,
            FullName = name,
            Phone = "+527711234567",
            RentalMode = "individual",
            AcceptedTermsVersion = "1.0",
            AcceptedTermsAt = Now,
            CreatedAt = Now,
        };

    private static async Task<string> OpenConversationAsync(TestWorld world) =>
        (await world.Requests.CreateAsync(Student, world.RoomId, Body(["deposit"], null), CancellationToken.None)).ConversationId;

    private sealed record TestWorld(
        RentalRequestService Requests,
        ConversationService Chat,
        InMemoryRentalRequestRepository RequestStore,
        InMemoryConversationRepository ConversationStore,
        InMemoryRoomRepository Rooms,
        InMemoryLandlordProfileRepository Landlords,
        InMemoryVisitRepository Visits,
        InMemoryModerationRepositories Moderation,
        InMemoryUserLock Lock,
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
        var moderation = new InMemoryModerationRepositories();
        var userLock = new InMemoryUserLock();
        var requestStore = new InMemoryRentalRequestRepository();
        var conversationStore = new InMemoryConversationRepository();
        var clock = new ManualTimeProvider(Now);

        landlords.CreateAsync(Landlord(Owner, "Laura Gómez Pérez"), CancellationToken.None).GetAwaiter().GetResult();
        profiles.Profiles[Student] = new UserProfile { UserId = Student, Name = "Ana", UniversityId = UniversityStub.UtttId };

        var ids = new List<string>();
        for (var index = 0; index < rooms; index++)
        {
            var building = buildings.CreateAsync(
                new Building
                {
                    OwnerId = Owner,
                    Name = index == 0 ? "Casa Azul" : $"Casa {index}",
                    Address = new BuildingAddress("Calle", "1", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                    Latitude = 20.05,
                    Longitude = -99.34,
                    Status = BuildingStatus.Verified,
                    Version = 1,
                },
                CancellationToken.None).GetAwaiter().GetResult();
            var room = roomStore.CreateManyAsync(
                [new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101, Floor = 1, Price = 2500, Status = RoomStatus.Available, Version = 1 }],
                CancellationToken.None).GetAwaiter().GetResult()[0];
            ids.Add(room.Id);
        }

        var suspensions = new AccountSuspensionQuery(moderation);
        var universities = new UniversityStub();
        return new TestWorld(
            new RentalRequestService(requestStore, conversationStore, roomStore, buildings, landlords, profiles, universities, visits, suspensions, userLock, clock),
            new ConversationService(conversationStore, roomStore, buildings, landlords, profiles, suspensions, clock),
            requestStore,
            conversationStore,
            roomStore,
            landlords,
            visits,
            moderation,
            userLock,
            ids);
    }

    private sealed class UniversityStub : IUniversityRepository
    {
        public const string UtttId = "6650000000000000000000aa";

        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<University?>(id == UtttId ? new University { Id = UtttId, Code = "UTTT", Name = "Universidad Tecnológica de Tula-Tepeji" } : null);

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
