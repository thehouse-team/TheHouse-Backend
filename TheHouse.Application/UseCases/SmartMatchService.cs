using System.Globalization;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Application.Contracts.Search;
using TheHouse.Application.Contracts.SmartMatch;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// Perfil sin universidad, `budget.max`, `livingWith` o `placeType` (422 con `missing`).
public sealed class SmartMatchProfileIncompleteException(IReadOnlyList<string> missing) : Exception("Smart Match profile is incomplete.")
{
    public IReadOnlyList<string> Missing { get; } = missing;
}

// NV-23 1.2.0: Smart Match con el modelo base `baseline-1.0.0` de TheHouse-ML.
// Solo lee preferencias de búsqueda y atributos públicos del cuarto (RNF-11);
// los puntajes no se guardan (RF-58).
public sealed class SmartMatchService(
    IPreferencesRepository preferences,
    IProfileRepository profiles,
    IUniversityRepository universities,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    SearchService search,
    ISmartMatchRepository store,
    ICompatibilityModel model,
    StaffService staff,
    TimeProvider timeProvider,
    ISmartMatchStatsSink? stats = null)
{
    public async Task<SmartMatchScoresResponse> ScoresAsync(string? userId, SmartMatchScoresRequest request, CancellationToken cancellationToken)
    {
        var ids = RoomIds(request.RoomIds);
        var (student, university) = userId is null
            ? await GuestAsync(request.UniversityId, request.Preferences, cancellationToken)
            : request.UniversityId is not null || request.Preferences is not null
                ? throw new RequestValidationException("body", "With a session only roomIds are accepted.")
                : await UserAsync(userId, cancellationToken);

        var found = (await rooms.GetManyAsync(ids, cancellationToken)).ToDictionary(room => room.Id, StringComparer.Ordinal);
        var buildingIds = found.Values.Select(room => room.BuildingId).Distinct(StringComparer.Ordinal).ToArray();
        var owners = (await buildings.GetManyAsync(buildingIds, cancellationToken)).ToDictionary(building => building.Id, StringComparer.Ordinal);

        var items = new List<SmartMatchScoreItem>(ids.Count);
        var scored = new List<SmartMatchScore>();
        var evaluated = 0;
        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var room) ||
                !owners.TryGetValue(room.BuildingId, out var building) ||
                room.Status != RoomStatus.Available ||
                building.Status != BuildingStatus.Verified ||
                room.Price is null)
            {
                // Sin revelar si el cuarto existió.
                items.Add(Excluded(id, [SmartMatchRules.Unavailable]));
                continue;
            }

            evaluated++;
            var (cellLat, cellLng) = ApproximateLocation.Cell(building.Latitude, building.Longitude);
            var distance = ListingRules.DistanceKm(cellLat, cellLng, university.Latitude!.Value, university.Longitude!.Value);
            var candidate = SmartMatchRules.Translate(room, building, distance);
            var violated = SmartMatchRules.Violations(student, candidate);
            if (violated.Count > 0)
            {
                items.Add(Excluded(id, violated));
                continue;
            }

            var score = model.Score(SmartMatchRules.Features(student, candidate));
            scored.Add(score);
            items.Add(new SmartMatchScoreItem(id, true, [], score.MatchScore, Breakdown(score), score.ModelVersion, score.FeatureSchemaVersion));
        }

        await RecordAsync(scoreRequest: true, guest: userId is null, evaluated, scored, cancellationToken);
        return new SmartMatchScoresResponse(items);
    }

    public async Task<SmartMatchRecommendationsResponse> RecommendationsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (student, _) = await UserAsync(userId, cancellationToken);
        var dismissed = await store.ListDismissedRoomIdsAsync(userId, Now - SmartMatchRules.DismissalDuration, cancellationToken);
        return await RecommendAsync(student, dismissed, guest: false, page, pageSize, cancellationToken);
    }

    public async Task<SmartMatchRecommendationsResponse> QueryAsync(SmartMatchQueryRequest request, int page, int pageSize, CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var (student, _) = await GuestAsync(request.UniversityId, request.Preferences, cancellationToken);
        return await RecommendAsync(student, new HashSet<string>(StringComparer.Ordinal), guest: true, page, pageSize, cancellationToken);
    }

    public async Task FeedbackAsync(string userId, SmartMatchFeedbackRequest request, CancellationToken cancellationToken)
    {
        var roomId = RoomId(request.RoomId);
        var useful = request.Useful ?? throw new RequestValidationException("useful", "useful is required.");
        await EnsurePublishedAsync(roomId, cancellationToken);
        await store.SaveFeedbackAsync(userId, roomId, useful, model.ModelVersion, Now, cancellationToken);
    }

    public async Task DismissAsync(string userId, SmartMatchDismissalRequest request, CancellationToken cancellationToken)
    {
        var roomId = RoomId(request.RoomId);
        await EnsurePublishedAsync(roomId, cancellationToken);
        await store.AddDismissalAsync(userId, roomId, Now, cancellationToken);
    }

    // Idempotente: deshacer un descarte que no existe también responde 204.
    public Task UndismissAsync(string userId, string? roomId, CancellationToken cancellationToken) =>
        store.RemoveDismissalAsync(userId, RoomId(roomId), cancellationToken);

    // NV-24: puntaje y desglose de un solo cuarto con el mismo cálculo de
    // NV-23. 400 id inválido; 422 perfil incompleto; 404 cuarto no publicado
    // o no elegible (sin revelar cuál de los dos).
    public async Task<SmartMatchExplanationContext> ExplanationContextAsync(
        string? userId,
        string? roomId,
        string? universityId,
        PreferencesRequest? preferences,
        CancellationToken cancellationToken)
    {
        var id = RoomId(roomId);
        var (student, university) = userId is null
            ? await GuestAsync(universityId, preferences, cancellationToken)
            : await UserAsync(userId, cancellationToken);
        var room = await rooms.GetAsync(id, cancellationToken);
        var building = room is null ? null : await buildings.GetAsync(room.BuildingId, cancellationToken);
        if (room is not { Status: RoomStatus.Available, Price: not null } || building is not { Status: BuildingStatus.Verified })
        {
            throw new NotFoundException("room");
        }

        var (cellLat, cellLng) = ApproximateLocation.Cell(building.Latitude, building.Longitude);
        var distance = ListingRules.DistanceKm(cellLat, cellLng, university.Latitude!.Value, university.Longitude!.Value);
        var candidate = SmartMatchRules.Translate(room, building, distance);
        if (SmartMatchRules.Violations(student, candidate).Count > 0)
        {
            throw new NotFoundException("room");
        }

        return new SmartMatchExplanationContext(student, university, room, candidate, model.Score(SmartMatchRules.Features(student, candidate)));
    }

    // RF-66: solo staff con rol `admin`.
    public async Task<SmartMatchMetricsResponse> MetricsAsync(string actorId, string? from, string? to, CancellationToken cancellationToken)
    {
        await staff.RequireAsync(actorId, StaffRoles.Admin, cancellationToken);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var end = to is null ? today : Day("to", to);
        var start = from is null ? end.AddDays(1 - SmartMatchRules.DefaultMetricsDays) : Day("from", from);
        if (start > end || end.DayNumber - start.DayNumber + 1 > SmartMatchRules.MaximumMetricsDays)
        {
            throw new RequestValidationException("from", $"from must not exceed to and the range is at most {SmartMatchRules.MaximumMetricsDays} days.");
        }

        var days = await store.ListStatsAsync(Text(start), Text(end), cancellationToken);
        var feedback = await store.CountFeedbackAsync(
            new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            cancellationToken);
        var active = await store.CountActiveDismissalsAsync(Now - SmartMatchRules.DismissalDuration, cancellationToken);

        var eligible = days.Sum(day => day.EligibleRooms);
        var totals = new SmartMatchMetricsTotals(
            days.Sum(day => day.ScoreRequests),
            days.Sum(day => day.RecommendationRequests),
            days.Sum(day => day.GuestRequests),
            days.Sum(day => day.RoomsScored),
            eligible,
            Average(days.Sum(day => day.ScoreSum), eligible),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["0-39"] = days.Sum(day => day.Bucket0To39),
                ["40-59"] = days.Sum(day => day.Bucket40To59),
                ["60-79"] = days.Sum(day => day.Bucket60To79),
                ["80-100"] = days.Sum(day => day.Bucket80To100),
            },
            feedback.Where(item => item.Useful).Sum(item => item.Count),
            feedback.Where(item => !item.Useful).Sum(item => item.Count),
            active);

        var versions = days.Select(day => day.ModelVersion).Concat(feedback.Select(item => item.ModelVersion)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var byModel = versions.Select(version =>
        {
            var rows = days.Where(day => day.ModelVersion == version).ToArray();
            var rowsEligible = rows.Sum(day => day.EligibleRooms);
            return new SmartMatchModelMetrics(
                version,
                rows.Sum(day => day.RoomsScored),
                Average(rows.Sum(day => day.ScoreSum), rowsEligible),
                feedback.Where(item => item.ModelVersion == version && item.Useful).Sum(item => item.Count),
                feedback.Where(item => item.ModelVersion == version && !item.Useful).Sum(item => item.Count));
        }).ToArray();

        return new SmartMatchMetricsResponse(Text(start), Text(end), totals, byModel);
    }

    private DateTimeOffset Now => timeProvider.GetUtcNow();

    private async Task<SmartMatchRecommendationsResponse> RecommendAsync(
        SmartMatchStudent student,
        IReadOnlySet<string> dismissed,
        bool guest,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var candidates = (await search.SmartMatchCandidatesAsync(student.UniversityId, student.MaxDistanceKm, cancellationToken))
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.Item.Id, StringComparer.Ordinal)
            .Take(SmartMatchRules.MaximumCandidates)
            .ToArray();

        var ranked = new List<(RoomSearchItem Item, SmartMatchScore Score, bool Dismissed)>();
        foreach (var candidate in candidates)
        {
            var item = candidate.Item;
            var room = SmartMatchRules.Translate(item.Id, item.Price, item.RoomType, item.Bathroom, item.BuildingType, item.AllowedTenants, item.Services, candidate.DistanceKm);
            if (SmartMatchRules.Violations(student, room).Count > 0)
            {
                continue;
            }

            ranked.Add((item, model.Score(SmartMatchRules.Features(student, room)), dismissed.Contains(item.Id)));
        }

        // No descartados primero; puntaje sin redondear desc; `roomId` asc (ML-04 §6.3).
        var ordered = ranked
            .OrderBy(entry => entry.Dismissed)
            .ThenByDescending(entry => entry.Score.RawTotal)
            .ThenBy(entry => entry.Item.Id, StringComparer.Ordinal)
            .ToArray();
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var withThumbnails = await search.WithThumbnailsAsync([.. pageItems.Select(entry => entry.Item)], cancellationToken);
        var items = pageItems.Select((entry, index) => new SmartMatchRecommendation(
            withThumbnails[index],
            entry.Score.MatchScore,
            Breakdown(entry.Score),
            entry.Dismissed,
            entry.Score.ModelVersion,
            entry.Score.FeatureSchemaVersion)).ToArray();

        await RecordAsync(scoreRequest: false, guest, candidates.Length, [.. ranked.Select(entry => entry.Score)], cancellationToken);
        return new SmartMatchRecommendationsResponse(items, page, pageSize, ordered.Length, PropertyQueryValidation.CalculateTotalPages(ordered.Length, pageSize));
    }

    private async Task<(SmartMatchStudent Student, University University)> UserAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(userId, cancellationToken);
        var saved = await preferences.GetAsync(userId, cancellationToken);
        var university = profile?.UniversityId is { } id ? await universities.GetByIdAsync(id, cancellationToken) : null;
        var universityId = university is { Latitude: not null, Longitude: not null } ? university.Id : null;
        var (student, missing) = SmartMatchRules.Resolve(universityId, saved);
        return student is null ? throw new SmartMatchProfileIncompleteException(missing) : (student, university!);
    }

    private async Task<(SmartMatchStudent Student, University University)> GuestAsync(string? universityId, PreferencesRequest? request, CancellationToken cancellationToken)
    {
        // Mismas validaciones de formato que NV-11; nada se guarda.
        var validated = request is null ? null : PreferencesService.Validate("guest", request, Now);
        University? university = null;
        if (universityId is not null)
        {
            if (!UniversityQueryValidation.IsObjectId(universityId))
            {
                throw new RequestValidationException("universityId", "universityId must be a 24-character hexadecimal ObjectId.");
            }

            university = await universities.GetByIdAsync(universityId.ToLowerInvariant(), cancellationToken);
            if (university is not { Latitude: not null, Longitude: not null })
            {
                throw new NotFoundException("university");
            }
        }

        var (student, missing) = SmartMatchRules.Resolve(university?.Id, validated);
        return student is null ? throw new SmartMatchProfileIncompleteException(missing) : (student, university!);
    }

    private async Task EnsurePublishedAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(roomId, cancellationToken);
        var building = room is null ? null : await buildings.GetAsync(room.BuildingId, cancellationToken);
        if (room is not { Status: RoomStatus.Available } || building is not { Status: BuildingStatus.Verified })
        {
            throw new NotFoundException("room");
        }
    }

    private async Task RecordAsync(bool scoreRequest, bool guest, int evaluated, IReadOnlyList<SmartMatchScore> scored, CancellationToken cancellationToken)
    {
        if (stats is null)
        {
            return;
        }

        await stats.RecordAsync(
            new SmartMatchDailyStats(
                Text(DateOnly.FromDateTime(Now.UtcDateTime)),
                model.ModelVersion,
                scoreRequest ? 1 : 0,
                scoreRequest ? 0 : 1,
                guest ? 1 : 0,
                evaluated,
                scored.Count,
                scored.Sum(score => (long)score.MatchScore),
                scored.Count(score => score.MatchScore < 40),
                scored.Count(score => score.MatchScore is >= 40 and < 60),
                scored.Count(score => score.MatchScore is >= 60 and < 80),
                scored.Count(score => score.MatchScore >= 80)),
            cancellationToken);
    }

    private SmartMatchScoreItem Excluded(string roomId, IReadOnlyList<string> reasons) =>
        new(roomId, false, reasons, null, [], model.ModelVersion, model.FeatureSchemaVersion);

    private static IReadOnlyList<SmartMatchFactorResponse> Breakdown(SmartMatchScore score) =>
        [.. score.Breakdown.Select(factor => new SmartMatchFactorResponse(factor.Factor, factor.Label, factor.Points, factor.MaxPoints))];

    private static IReadOnlyList<string> RoomIds(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count is 0 or > SmartMatchRules.MaximumScoreRooms)
        {
            throw new RequestValidationException("roomIds", $"roomIds must have 1 to {SmartMatchRules.MaximumScoreRooms} ids.");
        }

        return [.. values.Select(RoomId).Distinct(StringComparer.Ordinal)];
    }

    private static string RoomId(string? value) =>
        PropertyQueryValidation.IsObjectId(value)
            ? value!.ToLowerInvariant()
            : throw new RequestValidationException("roomId", "roomId must be a 24-character hexadecimal ObjectId.");

    private static DateOnly Day(string field, string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new RequestValidationException(field, $"{field} must be YYYY-MM-DD.");

    private static string Text(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static double? Average(long sum, long count) => count == 0 ? null : Math.Round(sum / (double)count, 1, MidpointRounding.AwayFromZero);
}

// NV-24: lo que necesita la explicación de un cuarto (nunca textos del arrendador).
public sealed record SmartMatchExplanationContext(
    SmartMatchStudent Student,
    University University,
    Room Room,
    SmartMatchRoom Candidate,
    SmartMatchScore Score);
