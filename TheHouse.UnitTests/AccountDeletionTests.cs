using TheHouse.Application.Abstractions.Identity;
using TheHouse.Application.Common;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.TestDoubles;

namespace TheHouse.UnitTests;

// NV-10 1.2.0: reglas de la eliminación y participantes por módulo.
public sealed class AccountDeletionTests
{
    private const string User = "665f1c2a9b1e4a00000a11ce";
    private const string Other = "665f1c2a9b1e4a0000000b0b";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Paging_helper_reads_every_page()
    {
        var calls = 0;
        var all = await AccountDeletionRules.AllAsync(page =>
        {
            calls++;
            IReadOnlyList<int> items = page < 3 ? [page, page] : [page];
            return Task.FromResult((items, 5L));
        });

        Assert.Equal([1, 1, 2, 2, 3], all);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void Deleted_account_keeps_its_full_display_name()
    {
        Assert.Equal("Usuario eliminado", MessagingRules.FirstName(AccountDeletionRules.DisplayName));
        Assert.Equal("Ana", MessagingRules.FirstName("Ana López"));
        Assert.Equal("ELIMINAR", AccountDeletionRules.Confirmation);
    }

    [Theory]
    [InlineData("2026-11", "https://thehouse.example/privacidad", true)]
    [InlineData("2026-11", null, true)]
    [InlineData("2026-11", "http://thehouse.example/privacidad", false)]
    [InlineData("2026 11", "https://thehouse.example/privacidad", false)]
    [InlineData("", null, false)]
    public void Legal_options_are_validated(string version, string? url, bool valid) =>
        Assert.Equal(valid, new LegalOptions { PrivacyVersion = version, PrivacyUrl = url }.IsValid());

    [Fact]
    public async Task Active_rental_blocks_and_pending_agreement_is_cancelled()
    {
        var agreements = new InMemoryAgreementRepository();
        var participant = new RentalsDeletionParticipant(agreements);
        await agreements.CreateAsync(Agreement("a1", AgreementStatus.Pending, "r1"), CancellationToken.None);

        var check = await participant.CheckAsync(User, Now, CancellationToken.None);
        Assert.Empty(check.Blockers);
        Assert.Equal(1, check.Effects[AccountDeletionEffects.PendingAgreements]);

        await participant.EraseAsync(User, Now, CancellationToken.None);
        Assert.Equal(AgreementStatus.Cancelled, agreements.Agreements["a1"].Status);

        await agreements.CreateAsync(Agreement("a2", AgreementStatus.Active, "r2"), CancellationToken.None);
        var blocked = await participant.CheckAsync(User, Now, CancellationToken.None);
        Assert.Equal(new AccountDeletionBlocker(AccountDeletionEffects.ActiveRental, 1), Assert.Single(blocked.Blockers));
        Assert.Empty((await participant.CheckAsync("665f1c2a9b1e4a0000000ca1", Now, CancellationToken.None)).Blockers);
    }

    [Fact]
    public async Task Landlord_buildings_are_retired_with_their_files_and_profile_anonymized()
    {
        var landlords = new InMemoryLandlordProfileRepository();
        var buildings = new InMemoryBuildingRepository();
        var rooms = new InMemoryRoomRepository();
        var media = new InMemoryMediaRepository();
        await landlords.CreateAsync(new LandlordProfile { UserId = User, FullName = "Bob Pérez", Phone = "+527711234567", RentalMode = "company", Rfc = "XAXX010101000" }, CancellationToken.None);
        var mine = await buildings.CreateAsync(new Building { OwnerId = User, Name = "Casa Verde", Status = BuildingStatus.Verified, Version = 3 }, CancellationToken.None);
        var theirs = await buildings.CreateAsync(new Building { OwnerId = Other, Name = "Casa Azul", Status = BuildingStatus.Verified, Version = 1 }, CancellationToken.None);
        await rooms.CreateManyAsync([new Room { BuildingId = mine.Id, OwnerId = User, Number = 101, Status = RoomStatus.Available, Version = 1 }], CancellationToken.None);
        var participant = new ListingsDeletionParticipant(landlords, buildings, rooms, media);

        var check = await participant.CheckAsync(User, Now, CancellationToken.None);
        Assert.Equal(1, check.Effects[AccountDeletionEffects.Buildings]);
        Assert.Equal(1, check.Effects[AccountDeletionEffects.Rooms]);

        await participant.EraseAsync(User, Now, CancellationToken.None);
        await participant.EraseAsync(User, Now, CancellationToken.None);

        Assert.Equal(BuildingStatus.Deleted, buildings.Buildings[mine.Id].Status);
        Assert.Equal(4, buildings.Buildings[mine.Id].Version);
        Assert.Equal(BuildingStatus.Verified, buildings.Buildings[theirs.Id].Status);
        var profile = await landlords.GetAsync(User, CancellationToken.None);
        Assert.Equal(AccountDeletionRules.DisplayName, profile!.FullName);
        Assert.Equal(string.Empty, profile.Phone);
        Assert.Null(profile.Rfc);
        Assert.Equal(0, (await participant.CheckAsync(User, Now, CancellationToken.None)).Effects[AccountDeletionEffects.Buildings]);
    }

    [Fact]
    public async Task Profile_favorites_notifications_roommate_and_views_are_erased()
    {
        var profiles = new InMemoryProfileRepository();
        var preferences = new InMemoryPreferencesRepository();
        var favorites = new InMemoryFavoriteRepository();
        var notifications = new InMemoryNotificationRepository();
        var roommates = new InMemoryRoommateProfileRepository();
        var views = new InMemoryRoomViewRepository();
        await profiles.SaveNameAsync(User, "Alice Gómez", Now, CancellationToken.None);
        await profiles.SaveUniversityAsync(User, "u1", Now, CancellationToken.None);
        preferences.Preferences[User] = new UserPreferences { UserId = User };
        await favorites.AddAsync(new Favorite { UserId = User, RoomId = "r1", CreatedAt = Now }, CancellationToken.None);
        await favorites.AddAsync(new Favorite { UserId = Other, RoomId = "r1", CreatedAt = Now }, CancellationToken.None);
        notifications.Items.Add(new Notification { UserId = User, Type = NotificationTypes.RequestReceived, DedupeKey = "k1" });
        notifications.Items.Add(new Notification { UserId = Other, Type = NotificationTypes.RequestReceived, DedupeKey = "k2" });
        views.Seed(new Room { Id = "r1", BuildingId = "b1", OwnerId = User }, "2026-11-10", 2);
        views.Seed(new Room { Id = "r2", BuildingId = "b2", OwnerId = Other }, "2026-11-10", 1);

        IAccountDeletionParticipant[] participants =
        [
            new ProfileDeletionParticipant(profiles, preferences),
            new FavoritesDeletionParticipant(favorites),
            new NotificationsDeletionParticipant(notifications),
            new RoommatesDeletionParticipant(roommates),
            new AnalyticsDeletionParticipant(views),
        ];
        Assert.Equal(1, (await participants[1].CheckAsync(User, Now, CancellationToken.None)).Effects[AccountDeletionEffects.Favorites]);
        foreach (var participant in participants)
        {
            await participant.EraseAsync(User, Now, CancellationToken.None);
            await participant.EraseAsync(User, Now, CancellationToken.None);
        }

        var profile = await profiles.GetAsync(User, CancellationToken.None);
        Assert.Equal(AccountDeletionRules.DisplayName, profile!.Name);
        Assert.Null(profile.UniversityId);
        Assert.False(preferences.Preferences.ContainsKey(User));
        Assert.Empty(await favorites.ListAllAsync(User, CancellationToken.None));
        Assert.Single(await favorites.ListAllAsync(Other, CancellationToken.None));
        Assert.Equal(Other, Assert.Single(notifications.Items).UserId);
        Assert.Equal(Other, Assert.Single(views.Items).OwnerId);
    }

    private static Agreement Agreement(string id, AgreementStatus status, string roomId) =>
        new()
        {
            Id = id,
            RequestId = "q-" + id,
            RoomId = roomId,
            BuildingId = "b1",
            OwnerId = Other,
            StudentId = User,
            ConversationId = "c-" + id,
            Status = status,
            CreatedAt = Now,
            UpdatedAt = Now,
            ActivatedAt = status == AgreementStatus.Active ? Now : null,
        };
}
