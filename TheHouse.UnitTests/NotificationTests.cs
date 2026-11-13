using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-20: catálogo, textos sin datos sensibles, idempotencia, agrupación de
// mensajes, bandeja, contador, marcas y llamadas desde los emisores.
// Reloj fijo: 2026-11-12 10:00 en Ciudad de México.
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 16, 0, 0, TimeSpan.Zero);

    private const string Student = "665f1c2a9b1e4a00000a11ce";
    private const string Owner = "665f1c2a9b1e4a0000000b0b";
    private const string Other = "665f1c2a9b1e4a0000000ca1";

    // --- Catálogo y textos -------------------------------------------

    [Fact]
    public void Catalog_has_sixteen_types_in_three_categories()
    {
        Assert.Equal(16, NotificationTypes.Catalog.Count);
        Assert.All(NotificationTypes.Catalog.Values, entry => Assert.Contains(entry.Category, NotificationCategories.All));
        Assert.Equal(NotificationCategories.Visits, NotificationTypes.Catalog[NotificationTypes.VisitConfirmed].Category);
        Assert.Equal(NotificationTargets.Conversation, NotificationTypes.Catalog[NotificationTypes.MessageReceived].TargetKind);
        Assert.Equal(NotificationCategories.System, NotificationTypes.Catalog[NotificationTypes.BuildingSuspended].Category);
    }

    [Fact]
    public void Every_template_renders_in_spanish_with_the_local_time()
    {
        foreach (var type in NotificationTypes.Catalog.Keys)
        {
            var (title, body) = NotificationTemplates.Render(type, "Ana", "Casa Azul · Cuarto 101", Now.AddDays(2));
            Assert.False(string.IsNullOrWhiteSpace(title), type);
            Assert.False(string.IsNullOrWhiteSpace(body), type);
        }

        var (_, visit) = NotificationTemplates.Render(NotificationTypes.VisitConfirmed, "Bob", "Casa Azul · Cuarto 101", new DateTimeOffset(2026, 11, 14, 23, 30, 0, TimeSpan.Zero));
        Assert.Equal("Bob confirmó tu visita a Casa Azul · Cuarto 101 el 14 de noviembre a las 17:30.", visit);
        Assert.Throws<ArgumentOutOfRangeException>(() => NotificationTemplates.Render("otro", "x", "y", null));
    }

    [Fact]
    public async Task Publisher_uses_first_names_and_never_contact_or_address()
    {
        var world = World();

        await world.Publisher.NotifyAsync(Event(NotificationTypes.RequestReceived, Owner, studentActor: true, world), CancellationToken.None);
        await world.Publisher.NotifyAsync(Event(NotificationTypes.RequestAccepted, Student, studentActor: false, world), CancellationToken.None);

        var received = Assert.Single(world.Store.Items, item => item.UserId == Owner);
        Assert.Equal("Nueva solicitud", received.Title);
        Assert.Equal("Ana pidió informes de Casa Azul · Cuarto 101.", received.Body);
        Assert.Equal(NotificationCategories.Requests, received.Category);
        Assert.Equal(NotificationTargets.Request, received.TargetKind);
        var accepted = Assert.Single(world.Store.Items, item => item.UserId == Student);
        Assert.Equal("Bob aceptó tu solicitud de Casa Azul · Cuarto 101.", accepted.Body);
        foreach (var item in world.Store.Items)
        {
            Assert.DoesNotContain("Pérez", item.Body);
            Assert.DoesNotContain("López", item.Body);
            Assert.DoesNotContain("Calle Privada", item.Body);
            Assert.DoesNotContain("+52", item.Body);
            Assert.DoesNotContain(Student, item.Body);
        }
    }

    [Fact]
    public async Task Same_fact_is_not_duplicated_and_messages_are_grouped()
    {
        var world = World();
        var accepted = Event(NotificationTypes.RequestAccepted, Student, studentActor: false, world);
        var message = new NotificationEvent(NotificationTypes.MessageReceived, Student, "6a00000000000000000000c1", NotificationService.MessageKey("6a00000000000000000000c1"), RoomId: world.RoomId, LandlordId: Owner);

        await world.Publisher.NotifyAsync(accepted, CancellationToken.None);
        await world.Publisher.NotifyAsync(accepted, CancellationToken.None);
        await world.Publisher.NotifyAsync(message, CancellationToken.None);
        await world.Publisher.NotifyAsync(message, CancellationToken.None);
        await world.Publisher.NotifyAsync(message, CancellationToken.None);

        Assert.Single(world.Store.Items, item => item.Type == NotificationTypes.RequestAccepted);
        var grouped = Assert.Single(world.Store.Items, item => item.Type == NotificationTypes.MessageReceived);
        Assert.Equal(3, grouped.Count);

        await world.Publisher.ClearAsync(Student, NotificationService.MessageKey("6a00000000000000000000c1"), CancellationToken.None);
        await world.Publisher.NotifyAsync(message, CancellationToken.None);
        Assert.Equal(2, world.Store.Items.Count(item => item.Type == NotificationTypes.MessageReceived));
    }

    // --- Bandeja, contador y marcas ---------------------------------

    [Fact]
    public async Task Inbox_filters_counts_and_marks_as_read()
    {
        var world = World();
        await world.Publisher.NotifyAsync(Event(NotificationTypes.RequestAccepted, Student, studentActor: false, world), CancellationToken.None);
        await world.Publisher.NotifyAsync(
            new NotificationEvent(NotificationTypes.VisitConfirmed, Student, "6a00000000000000000000d1", "visit_confirmed:d1", RoomId: world.RoomId, LandlordId: Owner, At: Now.AddDays(1)),
            CancellationToken.None);
        var service = new NotificationService(world.Store, new ManualTimeProvider(Now));

        var counts = await service.CountAsync(Student, CancellationToken.None);
        Assert.Equal(new UnreadCountResponse(2, 1, 1, 0), counts);
        var visits = await service.ListAsync(Student, "true", "visits", 1, 20, CancellationToken.None);
        var notification = Assert.Single(visits.Items);
        Assert.Equal(new NotificationTarget("visit", "6a00000000000000000000d1"), notification.Target);

        await service.MarkReadAsync(Student, notification.Id, CancellationToken.None);
        await service.MarkReadAsync(Student, notification.Id, CancellationToken.None);
        Assert.Equal(1, (await service.CountAsync(Student, CancellationToken.None)).Unread);
        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkReadAsync(Other, notification.Id, CancellationToken.None));

        await service.MarkAllReadAsync(Student, new ReadAllNotificationsRequest { Category = "requests" }, CancellationToken.None);
        Assert.Equal(0, (await service.CountAsync(Student, CancellationToken.None)).Unread);
        Assert.Equal(2, (await service.ListAsync(Student, "false", null, 1, 20, CancellationToken.None)).TotalItems);
        Assert.Empty((await service.ListAsync(Other, null, null, 1, 20, CancellationToken.None)).Items);
    }

    [Theory]
    [InlineData("si", null)]
    [InlineData(null, "pagos")]
    public async Task Inbox_validates_filters(string? unread, string? category)
    {
        var service = new NotificationService(new InMemoryNotificationRepository(), new ManualTimeProvider(Now));

        await Assert.ThrowsAsync<RequestValidationException>(() => service.ListAsync(Student, unread, category, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_ids_and_categories_are_rejected()
    {
        var service = new NotificationService(new InMemoryNotificationRepository(), new ManualTimeProvider(Now));

        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkReadAsync(Student, "x", CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.MarkAllReadAsync(Student, new ReadAllNotificationsRequest { Category = "otra" }, CancellationToken.None));
    }

    // --- Emisores -----------------------------------------------------

    [Fact]
    public async Task Messaging_notifies_the_other_party_and_never_the_actor()
    {
        var world = World();
        var sink = new RecordingNotificationSink();
        var requests = new InMemoryRentalRequestRepository();
        var conversations = new InMemoryConversationRepository();
        var service = new RentalRequestService(
            requests, conversations, world.Rooms, world.Buildings, world.Landlords, world.Profiles, new NoUniversities(),
            new InMemoryVisitRepository(), new NoSuspensions(), new InMemoryUserLock(), new ManualTimeProvider(Now), sink);
        var chat = new ConversationService(conversations, world.Rooms, world.Buildings, world.Landlords, world.Profiles, new NoSuspensions(), new ManualTimeProvider(Now), sink);

        var created = await service.CreateAsync(Student, world.RoomId, new CreateRentalRequest { Message = "Hola" }, CancellationToken.None);
        await service.AcceptAsync(Owner, created.Id, CancellationToken.None);
        var (message, _) = await chat.SendAsync(Student, created.ConversationId, new SendMessageRequest { Text = "¿Puedo ir mañana?", ClientMessageId = Guid.NewGuid().ToString() }, CancellationToken.None);
        await chat.MarkReadAsync(Owner, created.ConversationId, new MarkReadRequest { LastMessageId = message.Id }, CancellationToken.None);

        Assert.Equal(
            new[] { NotificationTypes.RequestReceived, NotificationTypes.RequestAccepted, NotificationTypes.MessageReceived },
            sink.Events.Select(item => item.Type));
        Assert.Equal(new[] { Owner, Student, Owner }, sink.Events.Select(item => item.RecipientId));
        Assert.Equal(Student, sink.Events[0].StudentId);
        Assert.Equal(Owner, sink.Events[1].LandlordId);
        Assert.Equal(NotificationService.MessageKey(created.ConversationId), sink.Events[2].DedupeKey);
        Assert.DoesNotContain(sink.Events, item => item.DedupeKey.Contains("Puedo", StringComparison.Ordinal));
        Assert.Equal((Owner, NotificationService.MessageKey(created.ConversationId)), Assert.Single(sink.Cleared));
    }

    [Fact]
    public async Task Landlord_visit_actions_notify_the_student()
    {
        var world = World();
        var sink = new RecordingNotificationSink();
        var visits = new InMemoryVisitRepository();
        var service = new LandlordVisitService(visits, world.Rooms, world.Buildings, world.Landlords, world.Profiles, new NoUniversities(), new ManualTimeProvider(Now), sink);
        var visit = await visits.CreateAsync(
            new Visit { UserId = Student, RoomId = world.RoomId, ScheduledAt = Now.AddDays(2), Status = VisitStatus.Requested, CreatedAt = Now, UpdatedAt = Now },
            CancellationToken.None);

        await service.ConfirmAsync(Owner, visit.Id, CancellationToken.None);
        await service.CancelAsync(Owner, visit.Id, new Application.Contracts.Visits.LandlordCancelVisitRequest { Reason = "not_available" }, CancellationToken.None);

        Assert.Equal(new[] { NotificationTypes.VisitConfirmed, NotificationTypes.VisitCancelled }, sink.Events.Select(item => item.Type));
        Assert.All(sink.Events, item => Assert.Equal(Student, item.RecipientId));
        Assert.All(sink.Events, item => Assert.Equal(Owner, item.LandlordId));
    }

    // --- Apoyo -------------------------------------------------------

    private static NotificationEvent Event(string type, string recipient, bool studentActor, TestWorld world) =>
        new(type, recipient, "6a00000000000000000000a1", $"{type}:6a00000000000000000000a1", RoomId: world.RoomId, StudentId: studentActor ? Student : null, LandlordId: studentActor ? null : Owner);

    private sealed record TestWorld(
        NotificationPublisher Publisher,
        InMemoryNotificationRepository Store,
        InMemoryRoomRepository Rooms,
        InMemoryBuildingRepository Buildings,
        InMemoryLandlordProfileRepository Landlords,
        InMemoryProfileRepository Profiles,
        string RoomId);

    private static TestWorld World()
    {
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        var landlords = new InMemoryLandlordProfileRepository();
        var profiles = new InMemoryProfileRepository();
        landlords.CreateAsync(
            new LandlordProfile { UserId = Owner, FullName = "Bob Pérez", Phone = "+527711234567", RentalMode = "individual", AcceptedTermsVersion = "1.0", AcceptedTermsAt = Now, CreatedAt = Now },
            CancellationToken.None).GetAwaiter().GetResult();
        profiles.Profiles[Student] = new UserProfile { UserId = Student, Name = "Ana López" };
        var building = buildings.CreateAsync(
            new Building
            {
                OwnerId = Owner,
                Name = "Casa Azul",
                Address = new BuildingAddress("Calle Privada", "10", "Centro", "Tula de Allende", "Hidalgo", "42800"),
                Status = BuildingStatus.Verified,
                Version = 1,
            },
            CancellationToken.None).GetAwaiter().GetResult();
        var room = rooms.CreateManyAsync(
            [new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 }],
            CancellationToken.None).GetAwaiter().GetResult()[0];
        var store = new InMemoryNotificationRepository();
        var publisher = new NotificationPublisher(store, rooms, buildings, landlords, profiles, new ManualTimeProvider(Now));
        return new TestWorld(publisher, store, rooms, buildings, landlords, profiles, room.Id);
    }

    private sealed class NoSuspensions : Application.Abstractions.Security.IAccountSuspensionQuery
    {
        public Task<bool> IsSuspendedAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class NoUniversities : Application.Abstractions.Persistence.IUniversityRepository
    {
        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<University?>(null);

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
