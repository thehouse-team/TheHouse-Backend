using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Roommates;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-22: validación del perfil, coincidencias, reciprocidad, misma
// universidad, vencimiento y contacto por una conversación `roommate`.
// Reloj fijo: 2026-11-17 10:00 en Ciudad de México.
public sealed class RoommateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 16, 0, 0, TimeSpan.Zero);

    private const string Ana = "665f1c2a9b1e4a00000a11ce";
    private const string Beto = "665f1c2a9b1e4a0000000b0b";
    private const string Caro = "665f1c2a9b1e4a0000000ca1";
    private const string Dani = "665f1c2a9b1e4a0000000d0d";
    private const string Uttt = "6650000000000000000000aa";
    private const string Other = "6650000000000000000000bb";

    [Fact]
    public void Matches_count_shared_habits_plus_budget_overlap()
    {
        var a = Profile(Ana) with { Habits = new RoommateHabits("night", "weekly", "quiet", "no", "none", "occasional"), BudgetMin = 1500, BudgetMax = 2500 };
        var b = Profile(Beto) with { Habits = new RoommateHabits("night", "weekly", "social", "no", "cats", "occasional"), BudgetMin = 2000, BudgetMax = 3000 };
        var c = b with { BudgetMin = 3000, BudgetMax = 4000 };

        Assert.Equal(5, a.MatchesWith(b));
        Assert.Equal(4, a.MatchesWith(c));
        Assert.Equal(a.MatchesWith(b), b.MatchesWith(a));
        Assert.True(a.IsListed(Now));
        Assert.False((a with { Visible = false }).IsListed(Now));
        Assert.False(a.IsListed(Now + RoommateProfile.Lifetime));
        Assert.Equal(ConversationKinds.RoommateKey(Ana, Beto), ConversationKinds.RoommateKey(Beto, Ana));
        Assert.Equal("roommate:" + Beto + ":" + Ana, ConversationKinds.RoommateKey(Ana, Beto));
    }

    [Theory]
    [InlineData(400, 2000, "2026-12", 6, "weekly", "budgetMin")]
    [InlineData(3000, 2000, "2026-12", 6, "weekly", "budgetMin")]
    [InlineData(1500, 2000, "2026-10", 6, "weekly", "moveIn")]
    [InlineData(1500, 2000, "2027-12", 6, "weekly", "moveIn")]
    [InlineData(1500, 2000, "diciembre", 6, "weekly", "moveIn")]
    [InlineData(1500, 2000, "2026-12", 0, "weekly", "durationMonths")]
    [InlineData(1500, 2000, "2026-12", 6, "nunca", "habits.cleaning")]
    public void Profile_input_is_validated(int min, int max, string moveIn, int months, string cleaning, string field)
    {
        var input = new RoommateProfileInput
        {
            Visible = true, BudgetMin = min, BudgetMax = max, Zone = "walking", MoveIn = moveIn, DurationMonths = months, HasPlace = false,
            Habits = new RoommateHabitsInput { Schedule = "night", Cleaning = cleaning, Noise = "quiet", Smoking = "no", Pets = "none", Social = "rare" },
        };

        var exception = Assert.Throws<RequestValidationException>(() => RoommateRules.Apply(new RoommateProfile(), input, Now));

        Assert.Equal(field, Assert.Single(exception.Errors.Keys));
    }

    [Fact]
    public async Task Profile_uses_the_university_of_the_user_and_renews_on_update()
    {
        var world = World();
        world.Users.Profiles[Dani] = new UserProfile { UserId = Dani, Name = "Dani" };
        Assert.Equal("university_required", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.UpsertAsync(Dani, Input(), CancellationToken.None))).Reason);

        var (created, isNew) = await world.Service.UpsertAsync(Ana, Input(), CancellationToken.None);
        world.Clock.Now = Now.AddDays(10);
        var (updated, again) = await world.Service.UpsertAsync(Ana, Input(), CancellationToken.None);

        Assert.True(isNew);
        Assert.False(again);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(new RoommateUniversity(Uttt, "UTTT"), created.University);
        Assert.Equal(Now.AddDays(70), updated.ExpiresAt);
        await world.Service.DeleteMineAsync(Ana, CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.GetMineAsync(Ana, CancellationToken.None));
    }

    [Fact]
    public async Task List_is_reciprocal_same_university_and_ordered_by_matches()
    {
        var world = World();
        await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.ListAsync(Ana, Filter(), 1, 20, CancellationToken.None));
        await world.Service.UpsertAsync(Ana, Input("night", "quiet"), CancellationToken.None);
        await world.Service.UpsertAsync(Beto, Input("night", "quiet"), CancellationToken.None);
        await world.Service.UpsertAsync(Caro, Input("early", "social"), CancellationToken.None);
        world.Users.Profiles[Dani] = new UserProfile { UserId = Dani, Name = "Dani", UniversityId = Other };
        await world.Service.UpsertAsync(Dani, Input("night", "quiet"), CancellationToken.None);

        var list = await world.Service.ListAsync(Ana, Filter(), 1, 20, CancellationToken.None);

        Assert.Equal(new[] { "Beto", "Caro" }, list.Items.Select(item => item.Name));
        Assert.Equal(7, list.Items[0].Matches);
        Assert.Equal(5, list.Items[1].Matches);
        Assert.DoesNotContain(list.Items, item => item.Id == world.Roommates.Profiles.Values.Single(profile => profile.UserId == Ana).Id);

        world.Suspensions.Suspended.Add(Beto);
        Assert.Equal("Caro", Assert.Single((await world.Service.ListAsync(Ana, Filter(), 1, 20, CancellationToken.None)).Items).Name);
        Assert.Empty((await world.Service.ListAsync(Ana, Filter() with { HasPlace = true }, 1, 20, CancellationToken.None)).Items);
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.ListAsync(Ana, Filter() with { Zone = "lejos" }, 1, 20, CancellationToken.None));

        world.Clock.Now = Now.AddDays(61);
        await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.ListAsync(Ana, Filter(), 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Contact_opens_one_roommate_conversation_per_pair()
    {
        var world = World();
        await world.Service.UpsertAsync(Ana, Input(), CancellationToken.None);
        await world.Service.UpsertAsync(Beto, Input(), CancellationToken.None);
        var anaProfile = world.Roommates.Profiles.Values.Single(profile => profile.UserId == Ana).Id;
        var betoProfile = world.Roommates.Profiles.Values.Single(profile => profile.UserId == Beto).Id;

        var (first, created) = await world.Service.ContactAsync(Ana, betoProfile, new ContactRoommateRequest { Message = "Hola, ¿buscas roomie?" }, CancellationToken.None);
        var (reply, again) = await world.Service.ContactAsync(Beto, anaProfile, new ContactRoommateRequest { Message = "¡Sí!" }, CancellationToken.None);

        Assert.True(created);
        Assert.False(again);
        Assert.Equal(first.ConversationId, reply.ConversationId);
        var conversation = Assert.Single(world.Conversations.Conversations.Values);
        Assert.Equal(ConversationKinds.Roommate, conversation.Kind);
        Assert.Equal(string.Empty, conversation.RoomId);
        Assert.All(world.Conversations.Messages, message => Assert.Equal(MessageRoles.Student, message.SenderRole));
        Assert.Equal("own_profile", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.ContactAsync(Ana, anaProfile, new ContactRoommateRequest { Message = "yo" }, CancellationToken.None))).Reason);
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.ContactAsync(Ana, betoProfile, new ContactRoommateRequest { Message = " " }, CancellationToken.None));

        world.Suspensions.Suspended.Add(Beto);
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.ContactAsync(Ana, betoProfile, new ContactRoommateRequest { Message = "Hola" }, CancellationToken.None));
    }

    [Fact]
    public async Task Roommate_conversations_appear_without_room_and_with_the_first_name()
    {
        var world = World();
        await world.Service.UpsertAsync(Ana, Input(), CancellationToken.None);
        await world.Service.UpsertAsync(Beto, Input(), CancellationToken.None);
        var betoProfile = world.Roommates.Profiles.Values.Single(profile => profile.UserId == Beto).Id;
        await world.Service.ContactAsync(Ana, betoProfile, new ContactRoommateRequest { Message = "Hola" }, CancellationToken.None);

        var list = await world.Chat.ListAsync(Beto, 1, 20, CancellationToken.None);

        var item = Assert.Single(list.Items);
        Assert.Equal(ConversationKinds.Roommate, item.Kind);
        Assert.Null(item.Room);
        Assert.Equal(string.Empty, item.RequestId);
        Assert.Equal("Ana", item.Counterpart.Name);
        Assert.Equal(MessageRoles.Student, item.Counterpart.Role);
        Assert.Equal(1, item.UnreadCount);
    }

    // --- Apoyo -------------------------------------------------------

    private static RoommateFilter Filter() => new(null, null, null, null, null);

    private static RoommateProfileInput Input(string schedule = "night", string noise = "quiet") =>
        new()
        {
            Visible = true, BudgetMin = 1500, BudgetMax = 2500, Zone = "walking", MoveIn = "2027-01", DurationMonths = 4, HasPlace = false,
            Habits = new RoommateHabitsInput { Schedule = schedule, Cleaning = "weekly", Noise = noise, Smoking = "no", Pets = "none", Social = "occasional" },
        };

    private static RoommateProfile Profile(string userId) =>
        new() { Id = "6a00000000000000000000" + userId[^2..], UserId = userId, UniversityId = Uttt, Visible = true, ExpiresAt = Now.AddDays(30), UpdatedAt = Now };

    private sealed record TestWorld(
        RoommateService Service,
        ConversationService Chat,
        InMemoryRoommateProfileRepository Roommates,
        InMemoryProfileRepository Users,
        InMemoryConversationRepository Conversations,
        FakeSuspensions Suspensions,
        ManualTimeProvider Clock);

    private static TestWorld World()
    {
        var users = new InMemoryProfileRepository();
        foreach (var (id, name) in new[] { (Ana, "Ana López"), (Beto, "Beto Ruiz"), (Caro, "Caro Díaz") })
        {
            users.Profiles[id] = new UserProfile { UserId = id, Name = name, UniversityId = Uttt };
        }

        var roommates = new InMemoryRoommateProfileRepository();
        var conversations = new InMemoryConversationRepository();
        var suspensions = new FakeSuspensions();
        var clock = new ManualTimeProvider(Now);
        var chat = new ConversationService(conversations, new InMemoryRoomRepository(), new InMemoryBuildingRepository(), new InMemoryLandlordProfileRepository(), users, suspensions, clock);
        var service = new RoommateService(roommates, users, new Universities(), conversations, chat, suspensions, clock);
        return new TestWorld(service, chat, roommates, users, conversations, suspensions, clock);
    }

    private sealed class FakeSuspensions : IAccountSuspensionQuery
    {
        public HashSet<string> Suspended { get; } = new(StringComparer.Ordinal);

        public Task<bool> IsSuspendedAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Suspended.Contains(userId));
    }

    private sealed class Universities : IUniversityRepository
    {
        public Task<(IReadOnlyList<University> Items, long TotalItems)> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<University?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<University?>(new University { Id = id, Code = "UTTT", Name = id == Uttt ? "UTTT" : "Otra" });

        public Task<IReadOnlyList<University>> SearchByTextAsync(string normalizedQuery, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<University>> SearchNearAsync(double latitude, double longitude, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpsertSeedAsync(IReadOnlyCollection<University> universities, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
