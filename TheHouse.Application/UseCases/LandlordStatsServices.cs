using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TheHouse.Application.Abstractions.Analytics;
using TheHouse.Application.Abstractions.Media;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Analytics;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-25 (RF-86): reglas de las estadísticas del arrendador. Todo se cuenta al
// pedirlo sobre las colecciones de cada módulo; solo las vistas son nuevas.
public static class LandlordStatsRules
{
    public const int DefaultDays = 30;

    public const int MaximumDays = 90;

    public const int MaximumAgeDays = 365;

    public const int MaximumRecommendations = 20;

    public const int MinimumPhotos = 2;

    public const decimal PriceRatio = 1.2m;

    public const int MinimumComparables = 5;

    public const double NearbyKm = 5;

    public const int ViewsWithoutRequests = 30;

    public const int RecentDays = 30;

    public static readonly TimeSpan PendingRequestAge = TimeSpan.FromHours(48);

    public static readonly TimeSpan PendingVisitAge = TimeSpan.FromHours(24);

    public static readonly TimeSpan ViewRetention = TimeSpan.FromDays(400);

    // Ventana de horario de visitas que se revisa al contar visitas creadas.
    public static readonly TimeSpan VisitHorizon = TimeSpan.FromDays(180);

    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, VisitSchedulePolicy.MexicoCity).DateTime);

    public static string DayText(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Day(DateTimeOffset at) => DayText(Today(at));

    // Inicio del día en la Ciudad de México, en UTC.
    public static DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, VisitSchedulePolicy.MexicoCity.GetUtcOffset(local)).ToUniversalTime();
    }

    public static (DateOnly From, DateOnly To) Range(string? from, string? to, DateOnly today)
    {
        var end = string.IsNullOrEmpty(to) ? today : Parse(to, "to");
        var start = string.IsNullOrEmpty(from) ? end.AddDays(-(DefaultDays - 1)) : Parse(from, "from");
        if (end > today)
        {
            throw new RequestValidationException("to", "to must not be in the future.");
        }

        if (start > end || end.DayNumber - start.DayNumber + 1 > MaximumDays)
        {
            throw new RequestValidationException("from", "from must be before to and the range at most 90 days.");
        }

        return start.DayNumber < today.AddDays(-MaximumAgeDays).DayNumber
            ? throw new RequestValidationException("from", "from must be within the last 365 days.")
            : (start, end);
    }

    public static double? Conversion(int requests, int views) =>
        views == 0 ? null : Math.Round((double)requests / views, 3, MidpointRounding.AwayFromZero);

    // HMAC-SHA256 con la sal del día: no reversible y distinto cada día.
    public static string ActorHash(byte[] salt, string viewerId) =>
        Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(viewerId))).ToLowerInvariant();

    public static decimal Median(IReadOnlyList<int> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2m;
    }

    public static bool MissingDetails(Room room) =>
        room.SizeM2 is null || room.Furniture.Count == 0 || room.AvailableFrom is null;

    private static DateOnly Parse(string value, string field) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw new RequestValidationException(field, $"{field} must be a date YYYY-MM-DD.");
}

// NV-25 R01: registra una vista por persona, cuarto y día. No cuenta al
// dueño ni al staff. Puede lanzar; quien lo llama (el detalle) lo absorbe.
public sealed class RoomViewService(
    IRoomViewRepository views,
    IViewSaltStore salts,
    IStaffRoleRepository staff,
    TimeProvider timeProvider)
{
    public async Task<bool> RecordAsync(Room room, string viewerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(viewerId) || viewerId == room.OwnerId)
        {
            return false;
        }

        if (await staff.GetAsync(viewerId, cancellationToken) is { Roles.Count: > 0 })
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var day = LandlordStatsRules.Day(now);
        var salt = await salts.GetDailySaltAsync(day, cancellationToken);
        return await views.AddAsync(
            new RoomView
            {
                RoomId = room.Id,
                BuildingId = room.BuildingId,
                OwnerId = room.OwnerId,
                Day = day,
                ActorHash = LandlordStatsRules.ActorHash(salt, viewerId),
                CreatedAt = now,
            },
            cancellationToken);
    }
}

// NV-25 R02–R04: resumen, estadísticas por cuarto y sugerencias. Solo datos
// del arrendador del token; un edificio ajeno responde 404.
public sealed class LandlordStatsService(
    ILandlordProfileRepository landlords,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IRoomViewRepository views,
    IFavoriteRepository favorites,
    IVisitRepository visits,
    IRentalRequestRepository requests,
    IAgreementRepository agreements,
    IReviewRepository reviews,
    TimeProvider timeProvider,
    IMediaPort? media = null)
{
    private const int BatchSize = 100;

    private const int MaximumBatches = 50;

    public async Task<LandlordStatsResponse> SummaryAsync(string ownerId, string? from, string? to, string? buildingId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (start, end) = LandlordStatsRules.Range(from, to, LandlordStatsRules.Today(now));
        var scope = await ScopeAsync(ownerId, buildingId, cancellationToken);
        var length = end.DayNumber - start.DayNumber + 1;
        var previousStart = start.AddDays(-length);
        var activity = await ActivityAsync(ownerId, scope, previousStart, end, cancellationToken);

        var current = activity.Totals(start, end, _ => true);
        var previous = activity.Totals(previousStart, start.AddDays(-1), _ => true);
        var daily = new List<StatsDay>(length);
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var text = LandlordStatsRules.DayText(day);
            var dayEnd = LandlordStatsRules.StartOf(day.AddDays(1));
            var dayStart = LandlordStatsRules.StartOf(day);
            daily.Add(new StatsDay(
                text,
                activity.Views.Where(item => item.Day == text).Sum(item => item.Count),
                activity.Requests.Count(item => item.CreatedAt >= dayStart && item.CreatedAt < dayEnd)));
        }

        return new LandlordStatsResponse(
            LandlordStatsRules.DayText(start),
            LandlordStatsRules.DayText(end),
            scope.BuildingId,
            current,
            previous,
            daily,
            activity.Favorites.Count,
            now);
    }

    public async Task<RoomStatsListResponse> RoomsAsync(
        string ownerId,
        string? from,
        string? to,
        string? buildingId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var now = timeProvider.GetUtcNow();
        var (start, end) = LandlordStatsRules.Range(from, to, LandlordStatsRules.Today(now));
        var scope = await ScopeAsync(ownerId, buildingId, cancellationToken);
        var activity = await ActivityAsync(ownerId, scope, start, end, cancellationToken);
        var names = scope.Buildings.ToDictionary(building => building.Id, building => building.Name, StringComparer.Ordinal);

        var rows = scope.Rooms
            .Select(room => (Room: room, Totals: activity.Totals(start, end, roomId => roomId == room.Id)))
            .OrderByDescending(row => row.Totals.Views)
            .ThenByDescending(row => row.Totals.Requests)
            .ThenBy(row => names.GetValueOrDefault(row.Room.BuildingId), StringComparer.Ordinal)
            .ThenBy(row => row.Room.Number)
            .ToList();

        var items = new List<RoomStatsItem>();
        foreach (var (room, totals) in rows.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var averages = await reviews.AveragesByRoomAsync(room.Id, cancellationToken);
            items.Add(new RoomStatsItem(
                room.Id,
                room.BuildingId,
                names.GetValueOrDefault(room.BuildingId) ?? string.Empty,
                room.Number,
                ListingRules.RoomStatusText(room.Status),
                room.Price,
                totals.Views,
                totals.Favorites,
                totals.Requests,
                totals.Visits,
                totals.Agreements,
                totals.Conversion,
                averages.Count == 0
                    ? null
                    : new StatsRating(ReviewRules.Mean(averages.Cleanliness, averages.Location, averages.Value, averages.Condition, averages.Services), averages.Count)));
        }

        return new RoomStatsListResponse(items, page, pageSize, rows.Count, PropertyQueryValidation.CalculateTotalPages(rows.Count, pageSize));
    }

    public async Task<StatsRecommendationsResponse> RecommendationsAsync(string ownerId, string? buildingId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = LandlordStatsRules.Today(now);
        var recentStart = today.AddDays(-(LandlordStatsRules.RecentDays - 1));
        var scope = await ScopeAsync(ownerId, buildingId, cancellationToken);
        var activity = await ActivityAsync(ownerId, scope, recentStart, today, cancellationToken);
        var buildingById = scope.Buildings.ToDictionary(building => building.Id, StringComparer.Ordinal);
        var photos = media is null || scope.Buildings.Count == 0
            ? null
            : await media.SnapshotAsync(scope.Buildings.Select(building => building.Id).ToArray(), publicUrls: false, cancellationToken);
        var pendingVisits = await PendingVisitsAsync(scope, now, cancellationToken);
        var medians = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        var tips = new List<(int Rank, Building Building, Room Room, StatsRecommendation Tip)>();

        foreach (var room in scope.Rooms)
        {
            var building = buildingById[room.BuildingId];
            void Add(string code, string priority, string title, string detail) =>
                tips.Add((priority == "high" ? 0 : priority == "medium" ? 1 : 2, building, room, new StatsRecommendation(code, priority, building.Id, room.Id, title, detail)));

            var label = $"Cuarto {room.Number.ToString(CultureInfo.InvariantCulture)} de {building.Name}";
            var waiting = activity.Requests.Count(item =>
                item.RoomId == room.Id && item.Status == RentalRequestStatus.Pending && now - item.CreatedAt > LandlordStatsRules.PendingRequestAge);
            if (waiting > 0)
            {
                Add("pending_requests", "high", "Responde tus solicitudes",
                    $"{label}: {Count(waiting, "solicitud espera", "solicitudes esperan")} respuesta desde hace más de 48 horas.");
            }

            var unconfirmed = pendingVisits.Count(item => item.RoomId == room.Id && now - item.CreatedAt > LandlordStatsRules.PendingVisitAge);
            if (unconfirmed > 0)
            {
                Add("pending_visits", "high", "Confirma las visitas",
                    $"{label}: {Count(unconfirmed, "visita sin confirmar", "visitas sin confirmar")} desde hace más de 24 horas.");
            }

            if (room.Status != RoomStatus.Available || building.Status != BuildingStatus.Verified)
            {
                continue;
            }

            var photoCount = photos is null ? (int?)null : photos.RoomPhotos.GetValueOrDefault(room.Id)?.Count ?? 0;
            if (photoCount is { } countPhotos && countPhotos < LandlordStatsRules.MinimumPhotos)
            {
                Add("add_photos", "high", "Agrega fotos",
                    $"{label} tiene {Count(countPhotos, "foto", "fotos")}; los cuartos con al menos 2 fotos reciben más solicitudes.");
            }

            if (room.Price is { } price)
            {
                var median = await MedianNearAsync(building, room.Id, medians, cancellationToken);
                if (median is { } value && price > value * LandlordStatsRules.PriceRatio)
                {
                    var above = (int)Math.Round((price - value) / value * 100, MidpointRounding.AwayFromZero);
                    Add("price_above_median", "medium", "Revisa el precio",
                        $"{label} cuesta {price.ToString("N0", CultureInfo.InvariantCulture)} MXN, {above.ToString(CultureInfo.InvariantCulture)} % arriba de la mediana de cuartos cerca de la misma universidad ({value.ToString("N0", CultureInfo.InvariantCulture)} MXN).");
                }
            }

            if (LandlordStatsRules.MissingDetails(room))
            {
                Add("missing_details", "medium", "Completa los detalles",
                    $"{label} no tiene tamaño, muebles o fecha de disponibilidad; completarlos ayuda a los estudiantes a decidir.");
            }

            var recent = activity.Totals(recentStart, today, roomId => roomId == room.Id);
            if (recent.Views >= LandlordStatsRules.ViewsWithoutRequests && recent.Requests == 0)
            {
                Add("views_without_requests", "medium", "Muchas vistas, ninguna solicitud",
                    $"{label} tuvo {recent.Views.ToString(CultureInfo.InvariantCulture)} vistas en 30 días y ninguna solicitud; revisa fotos, precio y descripción.");
            }
        }

        var items = tips
            .OrderBy(tip => tip.Rank)
            .ThenBy(tip => tip.Building.Name, StringComparer.Ordinal)
            .ThenBy(tip => tip.Room.Number)
            .ThenBy(tip => tip.Tip.Code, StringComparer.Ordinal)
            .Take(LandlordStatsRules.MaximumRecommendations)
            .Select(tip => tip.Tip)
            .ToArray();
        return new StatsRecommendationsResponse(items);
    }

    // --- Apoyo -------------------------------------------------------

    private sealed record Scope(string? BuildingId, IReadOnlyList<Building> Buildings, IReadOnlyList<Room> Rooms);

    private async Task<Scope> ScopeAsync(string ownerId, string? buildingId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(ownerId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }

        var owned = await buildings.ListAllByOwnerAsync(ownerId, cancellationToken);
        string? filter = null;
        if (!string.IsNullOrEmpty(buildingId))
        {
            if (!PropertyQueryValidation.IsObjectId(buildingId))
            {
                throw new RequestValidationException("buildingId", "buildingId must be a 24-character hexadecimal ObjectId.");
            }

            filter = buildingId.ToLowerInvariant();
            if (owned.All(building => building.Id != filter))
            {
                throw new NotFoundException("building");
            }
        }

        var selected = owned.Where(building => filter is null || building.Id == filter).ToArray();
        var ids = selected.Select(building => building.Id).ToHashSet(StringComparer.Ordinal);
        var ownRooms = (await rooms.ListAllByOwnerAsync(ownerId, cancellationToken))
            .Where(room => ids.Contains(room.BuildingId))
            .ToArray();
        return new Scope(filter, selected, ownRooms);
    }

    private async Task<Activity> ActivityAsync(string ownerId, Scope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var roomIds = scope.Rooms.Select(room => room.Id).ToHashSet(StringComparer.Ordinal);
        var start = LandlordStatsRules.StartOf(from);
        var end = LandlordStatsRules.StartOf(to.AddDays(1));

        var viewCounts = (await views.CountAsync(ownerId, LandlordStatsRules.DayText(from), LandlordStatsRules.DayText(to), cancellationToken))
            .Where(item => roomIds.Contains(item.RoomId))
            .ToArray();
        var saved = roomIds.Count == 0
            ? []
            : (await favorites.ListByRoomsAsync(roomIds, cancellationToken)).Where(item => roomIds.Contains(item.RoomId)).ToArray();

        var ownRequests = new List<RentalRequest>();
        for (var page = 1; page <= MaximumBatches; page++)
        {
            var (items, total) = await requests.ListByOwnerAsync(ownerId, null, null, page, BatchSize, cancellationToken);
            ownRequests.AddRange(items.Where(item => roomIds.Contains(item.RoomId)));
            if ((long)page * BatchSize >= total)
            {
                break;
            }
        }

        var ownAgreements = new List<Agreement>();
        for (var page = 1; page <= MaximumBatches; page++)
        {
            var (items, total) = await agreements.ListByOwnerAsync(ownerId, null, null, page, BatchSize, cancellationToken);
            ownAgreements.AddRange(items.Where(item => roomIds.Contains(item.RoomId)));
            if ((long)page * BatchSize >= total)
            {
                break;
            }
        }

        var ownVisits = new List<Visit>();
        if (roomIds.Count > 0)
        {
            for (var page = 1; page <= MaximumBatches; page++)
            {
                var (items, total) = await visits.ListForRoomsAsync(roomIds, start, end + LandlordStatsRules.VisitHorizon, null, page, BatchSize, cancellationToken);
                ownVisits.AddRange(items.Where(item => item.CreatedAt >= start && item.CreatedAt < end));
                if ((long)page * BatchSize >= total)
                {
                    break;
                }
            }
        }

        return new Activity(viewCounts, saved, ownRequests, ownVisits, ownAgreements);
    }

    private async Task<IReadOnlyList<Visit>> PendingVisitsAsync(Scope scope, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (scope.Rooms.Count == 0)
        {
            return [];
        }

        var roomIds = scope.Rooms.Select(room => room.Id).ToArray();
        var pending = new List<Visit>();
        for (var page = 1; page <= MaximumBatches; page++)
        {
            var (items, total) = await visits.ListForRoomsAsync(roomIds, now, now + LandlordStatsRules.VisitHorizon, VisitStatus.Requested, page, BatchSize, cancellationToken);
            pending.AddRange(items);
            if ((long)page * BatchSize >= total)
            {
                break;
            }
        }

        return pending;
    }

    // Mediana de cuartos publicados cerca de la misma universidad (sin el
    // propio); null si hay menos de 5 para comparar.
    private async Task<decimal?> MedianNearAsync(Building building, string roomId, Dictionary<string, decimal?> cache, CancellationToken cancellationToken)
    {
        var key = building.Id + "|" + roomId;
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var near = (await buildings.ListVerifiedNearAsync(building.Latitude, building.Longitude, LandlordStatsRules.NearbyKm, cancellationToken))
            .Where(item => item.NearestUniversityId == building.NearestUniversityId)
            .Select(item => item.Id)
            .ToArray();
        var prices = near.Length == 0
            ? []
            : (await rooms.ListAvailableByBuildingsAsync(near, new RoomFilter(null, null, null, null), cancellationToken))
                .Where(item => item.Id != roomId && item.Price is not null)
                .Select(item => item.Price!.Value)
                .ToArray();
        var median = prices.Length >= LandlordStatsRules.MinimumComparables ? LandlordStatsRules.Median(prices) : (decimal?)null;
        cache[key] = median;
        return median;
    }

    private static string Count(int value, string one, string many) =>
        value.ToString(CultureInfo.InvariantCulture) + " " + (value == 1 ? one : many);

    private sealed record Activity(
        IReadOnlyList<RoomViewCount> Views,
        IReadOnlyList<Favorite> Favorites,
        IReadOnlyList<RentalRequest> Requests,
        IReadOnlyList<Visit> Visits,
        IReadOnlyList<Agreement> Agreements)
    {
        public StatsTotals Totals(DateOnly from, DateOnly to, Func<string, bool> room)
        {
            var start = LandlordStatsRules.StartOf(from);
            var end = LandlordStatsRules.StartOf(to.AddDays(1));
            var fromText = LandlordStatsRules.DayText(from);
            var toText = LandlordStatsRules.DayText(to);
            bool In(DateTimeOffset? at) => at is { } value && value >= start && value < end;

            var viewTotal = Views
                .Where(item => room(item.RoomId) && string.CompareOrdinal(item.Day, fromText) >= 0 && string.CompareOrdinal(item.Day, toText) <= 0)
                .Sum(item => item.Count);
            var requestTotal = Requests.Count(item => room(item.RoomId) && In(item.CreatedAt));
            return new StatsTotals(
                viewTotal,
                Favorites.Count(item => room(item.RoomId) && In(item.CreatedAt)),
                requestTotal,
                Requests.Count(item => room(item.RoomId) && item.Status == RentalRequestStatus.Accepted && In(item.UpdatedAt)),
                Visits.Count(item => room(item.RoomId) && In(item.CreatedAt)),
                Visits.Count(item => room(item.RoomId) && In(item.ConfirmedAt)),
                Agreements.Count(item => room(item.RoomId) && In(item.ActivatedAt)),
                LandlordStatsRules.Conversion(requestTotal, viewTotal));
        }
    }
}
