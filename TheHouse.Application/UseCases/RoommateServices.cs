using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Application.Contracts.Roommates;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-22: catálogos y validación del perfil de compañero.
public static class RoommateRules
{
    public const int MinimumBudget = 500;

    public const int MaximumBudget = 50000;

    public const int MaximumDurationMonths = 24;

    public const int MaximumAbout = 300;

    public const int MaximumMessage = 500;

    public const int MaximumCandidates = 500;

    public static readonly IReadOnlySet<string> Zones = Set("walking", "near_campus", "any");

    public static readonly IReadOnlySet<string> Schedules = Set("early", "night", "mixed");

    public static readonly IReadOnlySet<string> Cleaning = Set("daily", "weekly", "relaxed");

    public static readonly IReadOnlySet<string> Noise = Set("quiet", "moderate", "social");

    public static readonly IReadOnlySet<string> Smoking = Set("no", "outside", "yes");

    public static readonly IReadOnlySet<string> Pets = Set("none", "cats", "dogs", "any");

    public static readonly IReadOnlySet<string> Social = Set("rare", "occasional", "frequent");

    public static string Month(DateTimeOffset now) =>
        TimeZoneInfo.ConvertTime(now, VisitSchedulePolicy.MexicoCity).ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static string MonthOrThrow(string? value, string field)
    {
        return value is not null && DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? value
            : throw new RequestValidationException(field, $"{field} must be YYYY-MM.");
    }

    public static RoommateProfile Apply(RoommateProfile profile, RoommateProfileInput body, DateTimeOffset now)
    {
        var budgetMin = Range(body.BudgetMin, "budgetMin", MinimumBudget, MaximumBudget);
        var budgetMax = Range(body.BudgetMax, "budgetMax", MinimumBudget, MaximumBudget);
        if (budgetMin > budgetMax)
        {
            throw new RequestValidationException("budgetMin", "budgetMin must not exceed budgetMax.");
        }

        var month = Month(now);
        var limit = TimeZoneInfo.ConvertTime(now, VisitSchedulePolicy.MexicoCity).AddMonths(12).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var moveIn = MonthOrThrow(body.MoveIn, "moveIn");
        if (string.CompareOrdinal(moveIn, month) < 0 || string.CompareOrdinal(moveIn, limit) > 0)
        {
            throw new RequestValidationException("moveIn", "moveIn must be between this month and 12 months ahead.");
        }

        var habits = body.Habits ?? throw new RequestValidationException("habits", "habits is required.");
        var about = string.IsNullOrWhiteSpace(body.About) ? null : body.About.Trim();
        if (about is { Length: > MaximumAbout })
        {
            throw new RequestValidationException("about", "about must have at most 300 characters.");
        }

        return profile with
        {
            Visible = body.Visible ?? throw new RequestValidationException("visible", "visible is required."),
            BudgetMin = budgetMin,
            BudgetMax = budgetMax,
            Zone = OneOf(body.Zone, "zone", Zones),
            MoveIn = moveIn,
            DurationMonths = Range(body.DurationMonths, "durationMonths", 1, MaximumDurationMonths),
            HasPlace = body.HasPlace ?? throw new RequestValidationException("hasPlace", "hasPlace is required."),
            Habits = new RoommateHabits(
                OneOf(habits.Schedule, "habits.schedule", Schedules),
                OneOf(habits.Cleaning, "habits.cleaning", Cleaning),
                OneOf(habits.Noise, "habits.noise", Noise),
                OneOf(habits.Smoking, "habits.smoking", Smoking),
                OneOf(habits.Pets, "habits.pets", Pets),
                OneOf(habits.Social, "habits.social", Social)),
            About = about,
            ExpiresAt = now + RoommateProfile.Lifetime,
            UpdatedAt = now,
        };
    }

    private static int Range(int? value, string field, int minimum, int maximum) =>
        value is { } number && number >= minimum && number <= maximum
            ? number
            : throw new RequestValidationException(field, $"{field} must be {minimum} to {maximum}.");

    private static string OneOf(string? value, string field, IReadOnlySet<string> allowed) =>
        value is not null && allowed.Contains(value)
            ? value
            : throw new RequestValidationException(field, $"{field} must be one of: {string.Join(", ", allowed)}.");

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
}

// Filtros de la lista de compañeros.
public sealed record RoommateFilter(int? BudgetMax, string? Zone, string? MoveInFrom, string? MoveInTo, bool? HasPlace);

// NV-22 (RF-46, RF-47): perfil de compañero voluntario, lista recíproca de la
// misma universidad ordenada por coincidencias y contacto por el chat de NV-16.
public sealed class RoommateService(
    IRoommateProfileRepository profiles,
    IProfileRepository users,
    IUniversityRepository universities,
    IConversationRepository conversations,
    ConversationService chat,
    IAccountSuspensionQuery suspensions,
    TimeProvider timeProvider)
{
    public async Task<(MyRoommateProfileResponse Profile, bool Created)> UpsertAsync(string userId, RoommateProfileInput body, CancellationToken cancellationToken)
    {
        var universityId = (await users.GetAsync(userId, cancellationToken))?.UniversityId;
        if (string.IsNullOrEmpty(universityId))
        {
            throw new ConflictException("university_required");
        }

        var now = timeProvider.GetUtcNow();
        var current = await profiles.GetByUserAsync(userId, cancellationToken)
            ?? new RoommateProfile { Id = MessagingIds.New(now), UserId = userId, CreatedAt = now };
        var updated = RoommateRules.Apply(current, body, now) with { UniversityId = universityId };
        var (saved, created) = await profiles.UpsertAsync(updated, cancellationToken);
        return (await ToMineAsync(saved, cancellationToken), created);
    }

    public async Task<MyRoommateProfileResponse> GetMineAsync(string userId, CancellationToken cancellationToken) =>
        await ToMineAsync(await profiles.GetByUserAsync(userId, cancellationToken) ?? throw new NotFoundException("roommate_profile"), cancellationToken);

    public Task DeleteMineAsync(string userId, CancellationToken cancellationToken) => profiles.DeleteByUserAsync(userId, cancellationToken);

    public async Task<RoommateListResponse> ListAsync(string userId, RoommateFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        if (filter.BudgetMax is { } budget && (budget < RoommateRules.MinimumBudget || budget > RoommateRules.MaximumBudget))
        {
            throw new RequestValidationException("budgetMax", "budgetMax must be 500 to 50000.");
        }

        if (filter.Zone is not null && !RoommateRules.Zones.Contains(filter.Zone))
        {
            throw new RequestValidationException("zone", "zone must be walking, near_campus or any.");
        }

        var from = filter.MoveInFrom is null ? null : RoommateRules.MonthOrThrow(filter.MoveInFrom, "moveInFrom");
        var to = filter.MoveInTo is null ? null : RoommateRules.MonthOrThrow(filter.MoveInTo, "moveInTo");
        var mine = await RequireListedAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var candidates = new List<RoommateProfile>();
        foreach (var candidate in await profiles.ListListedAsync(mine.UniversityId, userId, now, RoommateRules.MaximumCandidates, cancellationToken))
        {
            if ((filter.BudgetMax is null || candidate.BudgetMin <= filter.BudgetMax) &&
                (filter.Zone is null || candidate.Zone == filter.Zone || candidate.Zone == "any") &&
                (from is null || string.CompareOrdinal(candidate.MoveIn, from) >= 0) &&
                (to is null || string.CompareOrdinal(candidate.MoveIn, to) <= 0) &&
                (filter.HasPlace is null || candidate.HasPlace == filter.HasPlace) &&
                !await suspensions.IsSuspendedAsync(candidate.UserId, cancellationToken))
            {
                candidates.Add(candidate);
            }
        }

        var ordered = candidates
            .Select(candidate => (Profile: candidate, Matches: mine.MatchesWith(candidate)))
            .OrderByDescending(item => item.Matches)
            .ThenByDescending(item => item.Profile.UpdatedAt)
            .ThenBy(item => item.Profile.Id, StringComparer.Ordinal)
            .ToArray();
        var university = await UniversityAsync(mine.UniversityId, cancellationToken);
        var items = new List<RoommateCard>();
        foreach (var (profile, matches) in ordered.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await ToCardAsync(profile, university, matches, cancellationToken));
        }

        return new RoommateListResponse(items, page, pageSize, ordered.Length, PropertyQueryValidation.CalculateTotalPages(ordered.Length, pageSize));
    }

    public async Task<RoommateCard> GetAsync(string userId, string? id, CancellationToken cancellationToken)
    {
        var mine = await RequireListedAsync(userId, cancellationToken);
        var other = await LoadVisibleAsync(mine, id, cancellationToken);
        return await ToCardAsync(other, await UniversityAsync(other.UniversityId, cancellationToken), mine.MatchesWith(other), cancellationToken);
    }

    // Abre o reutiliza la conversación `roommate` de la pareja y envía el mensaje.
    public async Task<(ContactRoommateResponse Response, bool Created)> ContactAsync(string userId, string? id, ContactRoommateRequest body, CancellationToken cancellationToken)
    {
        var message = body.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > RoommateRules.MaximumMessage)
        {
            throw new RequestValidationException("message", "message must have between 1 and 500 characters.");
        }

        var mine = await RequireListedAsync(userId, cancellationToken);
        MessagingRules.EnsureId(id, "id");
        if (string.Equals(id, mine.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("own_profile");
        }

        var other = await LoadVisibleAsync(mine, id, cancellationToken);
        if (await suspensions.IsSuspendedAsync(userId, cancellationToken) || await suspensions.IsSuspendedAsync(other.UserId, cancellationToken))
        {
            throw new ConflictException("participant_suspended");
        }

        var now = timeProvider.GetUtcNow();
        var (conversation, created) = await conversations.CreateIfAbsentAsync(
            new Conversation
            {
                Id = MessagingIds.New(now),
                RequestId = ConversationKinds.RoommateKey(userId, other.UserId),
                Kind = ConversationKinds.Roommate,
                RoomId = string.Empty,
                StudentId = userId,
                OwnerId = other.UserId,
                Status = ConversationStatus.Open,
                LastMessageAt = now,
                CreatedAt = now,
            },
            cancellationToken);
        await chat.SendAsync(userId, conversation.Id, new SendMessageRequest { Text = message, ClientMessageId = Guid.NewGuid().ToString("D") }, cancellationToken);
        return (new ContactRoommateResponse(conversation.Id), created);
    }

    private async Task<RoommateProfile> RequireListedAsync(string userId, CancellationToken cancellationToken)
    {
        var mine = await profiles.GetByUserAsync(userId, cancellationToken);
        return mine is not null && mine.IsListed(timeProvider.GetUtcNow()) ? mine : throw new ForbiddenException("roommate_profile_required");
    }

    // Visible, vigente, de la misma universidad y no suspendido; si no, 404.
    private async Task<RoommateProfile> LoadVisibleAsync(RoommateProfile mine, string? id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        var other = await profiles.GetAsync(id!.ToLowerInvariant(), cancellationToken);
        return other is not null && other.UserId != mine.UserId && other.IsListed(timeProvider.GetUtcNow()) && other.UniversityId == mine.UniversityId &&
               !await suspensions.IsSuspendedAsync(other.UserId, cancellationToken)
            ? other
            : throw new NotFoundException("roommate");
    }

    private async Task<RoommateUniversity> UniversityAsync(string universityId, CancellationToken cancellationToken) =>
        new(universityId, (await universities.GetByIdAsync(universityId, cancellationToken))?.Name ?? string.Empty);

    private async Task<RoommateCard> ToCardAsync(RoommateProfile profile, RoommateUniversity university, int matches, CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(profile.UserId, cancellationToken);
        return new RoommateCard(
            profile.Id,
            string.IsNullOrWhiteSpace(user?.Name) ? "Estudiante" : MessagingRules.FirstName(user!.Name),
            university,
            profile.BudgetMin,
            profile.BudgetMax,
            profile.Zone,
            profile.MoveIn,
            profile.DurationMonths,
            profile.HasPlace,
            Habits(profile.Habits),
            profile.About,
            matches,
            profile.UpdatedAt);
    }

    private async Task<MyRoommateProfileResponse> ToMineAsync(RoommateProfile profile, CancellationToken cancellationToken) =>
        new(
            profile.Id,
            profile.Visible,
            profile.BudgetMin,
            profile.BudgetMax,
            profile.Zone,
            profile.MoveIn,
            profile.DurationMonths,
            profile.HasPlace,
            Habits(profile.Habits),
            profile.About,
            await UniversityAsync(profile.UniversityId, cancellationToken),
            profile.ExpiresAt,
            profile.CreatedAt,
            profile.UpdatedAt);

    private static RoommateHabitsResponse Habits(RoommateHabits habits) =>
        new(habits.Schedule, habits.Cleaning, habits.Noise, habits.Smoking, habits.Pets, habits.Social);
}
