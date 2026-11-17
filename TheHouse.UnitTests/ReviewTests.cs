using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Reviews;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-19: elegibilidad (visitó o rentó), crear, editar 30 días, promedios de
// las publicadas, mis reseñas y estados de moderación. Reloj fijo:
// 2026-11-13 10:00 en Ciudad de México.
public sealed class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 16, 0, 0, TimeSpan.Zero);

    private const string Student = "665f1c2a9b1e4a00000a11ce";
    private const string Owner = "665f1c2a9b1e4a0000000b0b";
    private const string Other = "665f1c2a9b1e4a0000000ca1";

    // --- Dominio ---------------------------------------------------

    [Fact]
    public void Averages_round_to_one_decimal_and_edits_close_after_30_days()
    {
        var review = Published(Student, "r1", Now) with { Landlord = new LandlordScores(5, 4, 4, 4), Property = new PropertyScores(5, 5, 4, 4, 4) };

        Assert.Equal(4.3, review.Landlord.Average);
        Assert.Equal(4.4, review.Property.Average);
        var edited = review.Edit(null, new PropertyScores(1, 1, 1, 1, 1), "Cambió", changeComment: true, Now.AddDays(29));
        Assert.True(edited.Edited);
        Assert.Equal(review.Landlord, edited.Landlord);
        Assert.Equal("Cambió", edited.Comment);
        Assert.Equal("review_locked", Assert.Throws<ConflictException>(() => review.Edit(null, null, null, false, Now.AddDays(30).AddSeconds(1))).Reason);
    }

    [Fact]
    public void Hide_and_unhide_follow_the_state()
    {
        var hidden = Published(Student, "r1", Now).Hide(Now);

        Assert.Equal(ReviewStatus.Hidden, hidden.Status);
        Assert.Equal("review_state", Assert.Throws<ConflictException>(() => hidden.Hide(Now)).Reason);
        Assert.Equal(ReviewStatus.Published, hidden.Unhide(Now).Status);
        Assert.Equal("review_state", Assert.Throws<ConflictException>(() => Published(Student, "r1", Now).Unhide(Now)).Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public void Scores_must_be_1_to_5(int? score)
    {
        Assert.Throws<RequestValidationException>(() => ReviewRules.Score(score, "x"));
        Assert.Throws<RequestValidationException>(() => ReviewRules.Comment(new string('a', 1001)));
        Assert.Null(ReviewRules.Comment("   "));
    }

    // --- Elegibilidad -------------------------------------------------

    [Fact]
    public async Task Eligibility_requires_a_past_confirmed_visit_or_a_rental()
    {
        var world = World();
        Assert.Equal("own_room", (await world.Service.EligibilityAsync(Owner, world.RoomId, CancellationToken.None)).Reason);
        Assert.Equal("no_stay", (await world.Service.EligibilityAsync(Student, world.RoomId, CancellationToken.None)).Reason);

        await world.Visits.CreateAsync(Visit(Student, world.RoomId, Now.AddDays(1), VisitStatus.Confirmed), CancellationToken.None);
        await world.Visits.CreateAsync(Visit(Student, world.RoomId, Now.AddDays(-2), VisitStatus.Cancelled), CancellationToken.None);
        Assert.False((await world.Service.EligibilityAsync(Student, world.RoomId, CancellationToken.None)).Eligible);

        await world.Visits.CreateAsync(Visit(Student, world.RoomId, Now.AddDays(-1), VisitStatus.Confirmed), CancellationToken.None);
        var visited = await world.Service.EligibilityAsync(Student, world.RoomId, CancellationToken.None);
        Assert.True(visited.Eligible);
        Assert.Equal(ReviewBases.Visit, visited.Basis);

        await world.Agreements.CreateAsync(Agreement(Other, world.RoomId, AgreementStatus.Ended), CancellationToken.None);
        Assert.Equal(ReviewBases.Rental, (await world.Service.EligibilityAsync(Other, world.RoomId, CancellationToken.None)).Basis);
        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.EligibilityAsync(Student, "6a00000000000000000000ff", CancellationToken.None));
    }

    // --- Crear, editar y listar --------------------------------------

    [Fact]
    public async Task Create_validates_criteria_and_allows_one_review_per_room()
    {
        var world = World();
        await world.Visits.CreateAsync(Visit(Student, world.RoomId, Now.AddDays(-1), VisitStatus.Confirmed), CancellationToken.None);

        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.CreateAsync(Student, world.RoomId, new ReviewInput { Landlord = AllFour(5) }, CancellationToken.None));
        Assert.Equal("no_stay", (await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.CreateAsync(Other, world.RoomId, Body(4), CancellationToken.None))).Reason);
        Assert.Equal("own_room", (await Assert.ThrowsAsync<ForbiddenException>(() => world.Service.CreateAsync(Owner, world.RoomId, Body(4), CancellationToken.None))).Reason);

        var created = await world.Service.CreateAsync(Student, world.RoomId, Body(4, "Muy limpio"), CancellationToken.None);

        Assert.Equal("Ana", created.Author.Name);
        Assert.Equal(ReviewBases.Visit, created.Basis);
        Assert.Equal(4, created.LandlordRating);
        Assert.Equal("Casa Azul · Cuarto 101", created.Room.Title);
        Assert.Equal(Owner, world.Reviews.Reviews[created.Id].OwnerId);
        Assert.Equal("review_exists", (await Assert.ThrowsAsync<ConflictException>(() => world.Service.CreateAsync(Student, world.RoomId, Body(4), CancellationToken.None))).Reason);
        var eligibility = await world.Service.EligibilityAsync(Student, world.RoomId, CancellationToken.None);
        Assert.Equal("already_reviewed", eligibility.Reason);
        Assert.Equal(created.Id, eligibility.ReviewId);
        Assert.Equal(Now.AddDays(30), eligibility.EditableUntil);
    }

    [Fact]
    public async Task Only_the_author_edits_within_30_days()
    {
        var world = World();
        await world.Visits.CreateAsync(Visit(Student, world.RoomId, Now.AddDays(-1), VisitStatus.Confirmed), CancellationToken.None);
        var created = await world.Service.CreateAsync(Student, world.RoomId, Body(4), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => world.Service.UpdateAsync(Other, created.Id, new ReviewInput { Comment = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() => world.Service.UpdateAsync(Student, created.Id, new ReviewInput(), CancellationToken.None));
        world.Clock.Now = Now.AddDays(10);
        var edited = await world.Service.UpdateAsync(Student, created.Id, new ReviewInput { Property = new PropertyScoresInput { Cleanliness = 5, Location = 5, Value = 5, Condition = 5, Services = 5 } }, CancellationToken.None);
        Assert.True(edited.Edited);
        Assert.Equal(5, edited.PropertyRating);
        Assert.Equal(4, edited.LandlordRating);

        world.Clock.Now = Now.AddDays(31);
        Assert.Equal("review_locked", (await Assert.ThrowsAsync<ConflictException>(
            () => world.Service.UpdateAsync(Student, created.Id, new ReviewInput { Comment = "tarde" }, CancellationToken.None))).Reason);
    }

    [Fact]
    public async Task List_shows_published_reviews_with_room_and_landlord_averages()
    {
        var world = World(rooms: 2);
        world.Reviews.Reviews["r1"] = Published(Student, world.RoomIds[0], Now.AddDays(-2)) with { Id = "r1", Landlord = new LandlordScores(5, 5, 5, 5), Property = new PropertyScores(4, 4, 4, 4, 4) };
        world.Reviews.Reviews["r2"] = Published(Other, world.RoomIds[0], Now.AddDays(-1)) with { Id = "r2", Landlord = new LandlordScores(3, 3, 3, 3), Property = new PropertyScores(2, 2, 2, 2, 2) };
        world.Reviews.Reviews["r3"] = Published(Student, world.RoomIds[1], Now) with { Id = "r3", Landlord = new LandlordScores(4, 4, 4, 4), Property = new PropertyScores(5, 5, 5, 5, 5) };
        world.Reviews.Reviews["r4"] = Published("665f1c2a9b1e4a0000000d0d", world.RoomIds[0], Now) with { Id = "r4", Landlord = new LandlordScores(1, 1, 1, 1), Status = ReviewStatus.Hidden };

        var list = await world.Service.ListAsync(world.RoomIds[0], 1, 10, CancellationToken.None);

        Assert.Equal(new[] { "r2", "r1" }, list.Items.Select(item => item.Id));
        Assert.Equal(new ReviewAverage(3, 2), list.Summary.Property);
        Assert.Equal(new ReviewAverage(4, 3), list.Summary.Landlord);
        Assert.Equal(4, list.Summary.Criteria.Communication);
        var empty = await World().Service.ListAsync(World().RoomId, 1, 10, CancellationToken.None);
        Assert.Equal(new ReviewAverage(null, 0), empty.Summary.Property);
    }

    [Fact]
    public async Task My_reviews_include_hidden_ones_with_their_status()
    {
        var world = World();
        world.Reviews.Reviews["r1"] = Published(Student, world.RoomId, Now) with { Id = "r1", Status = ReviewStatus.Hidden };

        var mine = await world.Service.ListMineAsync(Student, 1, 20, CancellationToken.None);

        Assert.Equal("hidden", Assert.Single(mine.Items).Status);
        Assert.Empty((await world.Service.ListAsync(world.RoomId, 1, 10, CancellationToken.None)).Items);
    }

    // --- Apoyo -------------------------------------------------------

    private static LandlordScoresInput AllFour(int score) => new() { Communication = score, Attention = score, Compliance = score, Respect = score };

    private static ReviewInput Body(int score, string? comment = null) =>
        new()
        {
            Landlord = AllFour(score),
            Property = new PropertyScoresInput { Cleanliness = score, Location = score, Value = score, Condition = score, Services = score },
            Comment = comment,
        };

    private static Review Published(string author, string roomId, DateTimeOffset at) =>
        new()
        {
            Id = "6a0000000000000000000099",
            RoomId = roomId,
            OwnerId = Owner,
            AuthorId = author,
            Basis = ReviewBases.Visit,
            Landlord = new LandlordScores(4, 4, 4, 4),
            Property = new PropertyScores(4, 4, 4, 4, 4),
            Status = ReviewStatus.Published,
            CreatedAt = at,
            UpdatedAt = at,
        };

    private static Visit Visit(string userId, string roomId, DateTimeOffset at, VisitStatus status) =>
        new() { UserId = userId, RoomId = roomId, ScheduledAt = at, Status = status, CreatedAt = Now.AddDays(-5), UpdatedAt = Now.AddDays(-5) };

    private static Agreement Agreement(string studentId, string roomId, AgreementStatus status) =>
        new()
        {
            Id = MessagingIds.New(Now),
            RoomId = roomId,
            OwnerId = Owner,
            StudentId = studentId,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private sealed record TestWorld(
        RoomReviewService Service,
        InMemoryReviewRepository Reviews,
        InMemoryVisitRepository Visits,
        InMemoryAgreementRepository Agreements,
        ManualTimeProvider Clock,
        IReadOnlyList<string> RoomIds)
    {
        public string RoomId => RoomIds[0];
    }

    private static TestWorld World(int rooms = 1)
    {
        var buildings = new InMemoryBuildingRepository();
        var roomStore = new InMemoryRoomRepository();
        var profiles = new InMemoryProfileRepository();
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
        var ids = new List<string>();
        for (var index = 0; index < rooms; index++)
        {
            ids.Add(roomStore.CreateManyAsync(
                [new Room { BuildingId = building.Id, OwnerId = Owner, Number = 101 + index, Floor = 1, Price = 3000, Status = RoomStatus.Available, Version = 1 }],
                CancellationToken.None).GetAwaiter().GetResult()[0].Id);
        }

        var reviews = new InMemoryReviewRepository();
        var visits = new InMemoryVisitRepository();
        var agreements = new InMemoryAgreementRepository();
        var clock = new ManualTimeProvider(Now);
        var service = new RoomReviewService(reviews, visits, new RentalHistoryQuery(agreements), roomStore, buildings, profiles, clock);
        return new TestWorld(service, reviews, visits, agreements, clock, ids);
    }
}
