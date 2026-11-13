using TheHouse.Application.Abstractions.Notifications;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Rentals;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-18: código de cierre. El código de respaldo usa 32 símbolos sin I, O, 0
// ni 1; el token del QR son 32 bytes aleatorios en base64url. Solo se guarda
// el SHA-256 de cada uno.
public static class ClosingCodes
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const int CodeLength = 8;

    public const int QrTokenLength = 43;

    public static (string BackupCode, string QrToken) Generate()
    {
        var code = RandomNumberGenerator.GetString(Alphabet, CodeLength);
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return (code, Base64Url.EncodeToString(bytes));
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // Acepta guiones, espacios y minúsculas; cualquier otro formato → 400.
    public static string NormalizeCode(string value)
    {
        var builder = new StringBuilder(CodeLength);
        foreach (var character in value)
        {
            if (character is '-' or ' ')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        var code = builder.ToString();
        return code.Length == CodeLength && code.All(Alphabet.Contains)
            ? code
            : throw new RequestValidationException("code", "code must have 8 characters.");
    }

    public static string NormalizeQrToken(string value) =>
        value.Length == QrTokenLength && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? value
            : throw new RequestValidationException("qrToken", "qrToken is not valid.");
}

// NV-18: reglas de los términos del convenio.
public static class AgreementRules
{
    public const int MinimumRent = 500;

    public const int MaximumRent = 100000;

    public const int MaximumDurationMonths = 24;

    public const int MaximumPaymentDay = 28;

    public const int MaximumNote = 300;

    public const int StartDaysBefore = 30;

    public const int StartDaysAfter = 90;

    public const string Currency = "MXN";

    public const string RentedNote = "El cuarto ya se rentó.";

    public static readonly IReadOnlySet<string> EndReasons = new HashSet<string>(StringComparer.Ordinal)
    {
        "contract_finished", "early_termination", "other",
    };

    public static string Wire(AgreementStatus status) => status.ToString().ToLowerInvariant();

    public static AgreementStatus? ParseStatus(string? value) => value switch
    {
        null or "" => null,
        "pending" => AgreementStatus.Pending,
        "active" => AgreementStatus.Active,
        "ended" => AgreementStatus.Ended,
        "cancelled" => AgreementStatus.Cancelled,
        _ => throw new RequestValidationException("status", "status must be pending, active, ended or cancelled."),
    };

    public static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, VisitSchedulePolicy.MexicoCity).DateTime);

    public static AgreementTerms Terms(CreateAgreementRequest body, DateOnly today)
    {
        var rent = body.MonthlyRent is { } value and >= MinimumRent and <= MaximumRent
            ? value
            : throw new RequestValidationException("monthlyRent", "monthlyRent must be 500 to 100000.");
        var deposit = body.Deposit is { } amount and >= 0 && amount <= 2 * rent
            ? amount
            : throw new RequestValidationException("deposit", "deposit must be 0 to twice the monthly rent.");
        if (body.StartDate is null ||
            !DateOnly.TryParseExact(body.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            start < today.AddDays(-StartDaysBefore) ||
            start > today.AddDays(StartDaysAfter))
        {
            throw new RequestValidationException("startDate", "startDate must be YYYY-MM-DD between 30 days ago and 90 days ahead.");
        }

        var months = body.DurationMonths is { } duration and >= 1 and <= MaximumDurationMonths
            ? duration
            : throw new RequestValidationException("durationMonths", "durationMonths must be 1 to 24.");
        var day = body.PaymentDay is { } payment and >= 1 and <= MaximumPaymentDay
            ? payment
            : throw new RequestValidationException("paymentDay", "paymentDay must be 1 to 28.");
        return new AgreementTerms(rent, deposit, start, months, start.AddMonths(months), day);
    }

    // Siguiente fecha con día `PaymentDay` desde hoy (no antes del inicio) y
    // antes de `EndDate`. Solo informativo: TheHouse no cobra (RES-03).
    public static DateOnly? NextPaymentDate(AgreementTerms terms, DateOnly today)
    {
        var from = today < terms.StartDate ? terms.StartDate : today;
        var candidate = new DateOnly(from.Year, from.Month, terms.PaymentDay);
        if (candidate < from)
        {
            candidate = candidate.AddMonths(1);
        }

        return candidate < terms.EndDate ? candidate : null;
    }

    public static string? Note(string? value)
    {
        var note = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return note is { Length: > MaximumNote }
            ? throw new RequestValidationException("note", "note must have at most 300 characters.")
            : note;
    }
}

// NV-18 (RF-81 a RF-84): cierre de trato con QR, Mi renta y fin de renta.
// Lo ajeno responde 404 (BOLA). Aceptar es un cambio condicional del
// convenio y del cuarto con compensación, bajo un candado por cuarto
// (M-BE-070).
public sealed class AgreementService(
    IAgreementRepository agreements,
    IRentalRequestRepository requests,
    IConversationRepository conversations,
    IRoomRepository rooms,
    IBuildingRepository buildings,
    ILandlordProfileRepository landlords,
    IProfileRepository profiles,
    IUniversityRepository universities,
    IVisitRepository visits,
    IUserLock userLock,
    TimeProvider timeProvider,
    INotificationSink? notifications = null)
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(10);

    private const int EffectAttempts = 3;

    private const int EffectPageSize = 50;

    public async Task<LandlordAgreementResponse> CreateAsync(string ownerId, CreateAgreementRequest body, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        MessagingRules.EnsureId(body.RequestId, "requestId");
        var now = timeProvider.GetUtcNow();
        var terms = AgreementRules.Terms(body, AgreementRules.Today(now));

        var request = await requests.GetAsync(body.RequestId!.ToLowerInvariant(), cancellationToken);
        if (request is null || request.OwnerId != ownerId)
        {
            throw new NotFoundException("request");
        }

        if (request.Status != RentalRequestStatus.Accepted)
        {
            throw new ConflictException("request_state");
        }

        var (room, building) = await LoadPublishedAsync(request.RoomId, cancellationToken);
        var (backupCode, qrToken) = ClosingCodes.Generate();
        var agreement = new Agreement
        {
            Id = MessagingIds.New(now),
            RequestId = request.Id,
            RoomId = room.Id,
            BuildingId = room.BuildingId,
            OwnerId = ownerId,
            StudentId = request.StudentId,
            ConversationId = request.ConversationId,
            Terms = terms,
            Status = AgreementStatus.Pending,
            Code = NewCode(backupCode, qrToken, now),
            History = [new AgreementHistoryEntry(AgreementStatus.Pending, now, AgreementActors.Landlord)],
            CreatedAt = now,
            UpdatedAt = now,
        };

        var created = await agreements.CreateAsync(agreement, cancellationToken) ?? throw new ConflictException("agreement_open");
        return await ToLandlordResponseAsync(created, room, building, new ClosingCodeResponse(qrToken, backupCode, created.Code!.ExpiresAt), cancellationToken);
    }

    public async Task<LandlordAgreementListResponse> ListAsync(
        string ownerId,
        string? status,
        string? roomId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        PropertyQueryValidation.EnsureValidPagination(page, pageSize);
        var filter = AgreementRules.ParseStatus(status);
        if (roomId is not null)
        {
            MessagingRules.EnsureId(roomId, "roomId");
        }

        var (items, total) = await agreements.ListByOwnerAsync(ownerId, filter, roomId?.ToLowerInvariant(), page, pageSize, cancellationToken);
        var responses = new List<LandlordAgreementResponse>(items.Count);
        foreach (var item in items)
        {
            responses.Add(await ToLandlordResponseAsync(item, null, cancellationToken));
        }

        return new LandlordAgreementListResponse(responses, page, pageSize, total, PropertyQueryValidation.CalculateTotalPages(total, pageSize));
    }

    public async Task<LandlordAgreementResponse> GetAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        return await ToLandlordResponseAsync(await LoadOwnedAsync(ownerId, id, cancellationToken), null, cancellationToken);
    }

    public async Task<LandlordAgreementResponse> IssueCodeAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var agreement = await LoadOwnedAsync(ownerId, id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var (backupCode, qrToken) = ClosingCodes.Generate();
        var updated = agreement.WithCode(NewCode(backupCode, qrToken, now), now);
        await SaveAsync(agreement, updated, cancellationToken);
        return await ToLandlordResponseAsync(updated, new ClosingCodeResponse(qrToken, backupCode, updated.Code!.ExpiresAt), cancellationToken);
    }

    public async Task<LandlordAgreementResponse> CancelAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var agreement = await LoadOwnedAsync(ownerId, id, cancellationToken);
        var cancelled = agreement.Cancel(timeProvider.GetUtcNow());
        await SaveAsync(agreement, cancelled, cancellationToken);
        return await ToLandlordResponseAsync(cancelled, null, cancellationToken);
    }

    // Fin de renta: el convenio pasa a `ended` y el cuarto vuelve a `available`.
    public async Task<LandlordAgreementResponse> EndAsync(string ownerId, string? id, EndAgreementRequest? body, CancellationToken cancellationToken)
    {
        await RequireLandlordAsync(ownerId, cancellationToken);
        var reason = body?.Reason;
        if (reason is not null && !AgreementRules.EndReasons.Contains(reason))
        {
            throw new RequestValidationException("reason", "reason must be contract_finished, early_termination or other.");
        }

        var note = AgreementRules.Note(body?.Note);
        var agreement = await LoadOwnedAsync(ownerId, id, cancellationToken);
        await using var handle = await LockRoomAsync(agreement.RoomId, cancellationToken);
        var current = await agreements.GetAsync(agreement.Id, cancellationToken) ?? throw new NotFoundException("agreement");
        var now = timeProvider.GetUtcNow();
        var ended = current.End(reason, note, now);
        await SaveAsync(current, ended, cancellationToken);
        await ReleaseRoomAsync(current.RoomId, now, cancellationToken);
        await NotifyAsync(
            new NotificationEvent(NotificationTypes.AgreementEnded, ended.StudentId, ended.Id, $"{NotificationTypes.AgreementEnded}:{ended.Id}", RoomId: ended.RoomId, LandlordId: ended.OwnerId),
            cancellationToken);
        return await ToLandlordResponseAsync(ended, null, cancellationToken);
    }

    // Revisar el convenio sin consumir el código.
    public async Task<AgreementPreviewResponse> PreviewAsync(string studentId, ClosingCodeRequest body, CancellationToken cancellationToken)
    {
        var agreement = await FindForStudentAsync(studentId, body.Code, body.QrToken, cancellationToken);
        agreement.EnsureCodeUsable(timeProvider.GetUtcNow());
        var room = await rooms.GetAsync(agreement.RoomId, cancellationToken);
        var building = await buildings.GetAsync(agreement.BuildingId, cancellationToken);
        var landlord = await landlords.GetAsync(agreement.OwnerId, cancellationToken);
        return new AgreementPreviewResponse(
            agreement.Id,
            RoomSummary(agreement, room, building),
            BuildingSummary(building),
            new AgreementLandlordSummary(MessagingRules.FirstName(landlord?.FullName)),
            TermsResponse(agreement.Terms),
            agreement.Code!.ExpiresAt);
    }

    // Aceptar: convenio activo, código usado y cuarto rentado. Devuelve si
    // quedaron efectos pendientes (otras solicitudes o visitas del cuarto)
    // para que el endpoint lo registre; la renta no se deshace por eso.
    public async Task<(RentalResponse Rental, bool EffectsPending)> AcceptAsync(string studentId, AcceptAgreementRequest body, CancellationToken cancellationToken)
    {
        if (body.AcceptedTerms != true)
        {
            throw new RequestValidationException("acceptedTerms", "acceptedTerms must be true.");
        }

        var found = await FindForStudentAsync(studentId, body.Code, body.QrToken, cancellationToken);
        found.EnsureCodeUsable(timeProvider.GetUtcNow());

        Agreement activated;
        await using (await LockRoomAsync(found.RoomId, cancellationToken))
        {
            var agreement = await agreements.GetAsync(found.Id, cancellationToken) ?? throw new NotFoundException("code", "code_invalid");
            if (agreement.Code?.CodeHash != found.Code?.CodeHash)
            {
                throw new NotFoundException("code", "code_invalid");
            }

            var now = timeProvider.GetUtcNow();
            agreement.EnsureCodeUsable(now);
            var request = await requests.GetAsync(agreement.RequestId, cancellationToken);
            if (request is null || request.Status != RentalRequestStatus.Accepted)
            {
                throw new ConflictException("request_state");
            }

            var (room, _) = await LoadPublishedAsync(agreement.RoomId, cancellationToken);
            activated = agreement.Activate(now);
            await SaveAsync(agreement, activated, cancellationToken);

            var rented = room.Rent(now) with { Version = room.Version + 1 };
            if (!await rooms.ReplaceAsync(rented, room.Version, cancellationToken))
            {
                // Compensación: el cuarto cambió a mitad; el convenio vuelve a
                // pendiente sin código.
                await agreements.ReplaceIfUnchangedAsync(activated.RevertActivation(timeProvider.GetUtcNow()), activated.UpdatedAt, cancellationToken);
                throw new ConflictException("room_unavailable");
            }
        }

        await NotifyAsync(
            new NotificationEvent(NotificationTypes.AgreementActivated, activated.OwnerId, activated.Id, $"{NotificationTypes.AgreementActivated}:{activated.Id}", RoomId: activated.RoomId, StudentId: activated.StudentId),
            cancellationToken);
        var effectsPending = !await ApplyRentedEffectsAsync(activated, cancellationToken);
        return (await ToRentalAsync(activated, cancellationToken), effectsPending);
    }

    public async Task<RentalResponse> GetMineAsync(string studentId, CancellationToken cancellationToken)
    {
        var agreement = await agreements.GetActiveForStudentAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("rental", "no_active_rental");
        return await ToRentalAsync(agreement, cancellationToken);
    }

    // Rechaza las demás solicitudes activas del cuarto y cancela las visitas
    // activas de otros estudiantes. Idempotente; reintenta antes de rendirse.
    public async Task<bool> ApplyRentedEffectsAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= EffectAttempts; attempt++)
        {
            try
            {
                await RejectOtherRequestsAsync(agreement, cancellationToken);
                await CancelOtherVisitsAsync(agreement, cancellationToken);
                return true;
            }
            catch (DependencyUnavailableException) when (attempt < EffectAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), timeProvider, cancellationToken);
            }
            catch (DependencyUnavailableException)
            {
                return false;
            }
        }

        return false;
    }

    private async Task RejectOtherRequestsAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        foreach (var status in new[] { RentalRequestStatus.Pending, RentalRequestStatus.Accepted })
        {
            var (items, _) = await requests.ListByOwnerAsync(agreement.OwnerId, status, agreement.RoomId, 1, EffectPageSize, cancellationToken);
            foreach (var request in items.Where(item => item.Id != agreement.RequestId))
            {
                var rejected = request with
                {
                    Status = RentalRequestStatus.Rejected,
                    Rejection = new RequestRejection("not_available", AgreementRules.RentedNote),
                    UpdatedAt = timeProvider.GetUtcNow(),
                };
                if (await requests.UpdateStatusAsync(rejected, status, cancellationToken))
                {
                    await conversations.CloseAsync(request.ConversationId, cancellationToken);
                    await NotifyAsync(
                        new NotificationEvent(NotificationTypes.RequestRejected, request.StudentId, request.Id, $"{NotificationTypes.RequestRejected}:{request.Id}", RoomId: request.RoomId, LandlordId: request.OwnerId),
                        cancellationToken);
                }
            }
        }
    }

    private async Task CancelOtherVisitsAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var status in new[] { VisitStatus.Requested, VisitStatus.Confirmed })
        {
            var (items, _) = await visits.ListForRoomsAsync([agreement.RoomId], now, now.AddDays(62), status, 1, EffectPageSize, cancellationToken);
            foreach (var visit in items.Where(item => item.UserId != agreement.StudentId && item.IsActive(now)))
            {
                if (await visits.ReplaceIfUnchangedAsync(visit.CancelByLandlord("not_available", AgreementRules.RentedNote, now), visit.UpdatedAt, cancellationToken))
                {
                    await NotifyAsync(
                        new NotificationEvent(NotificationTypes.VisitCancelled, visit.UserId, visit.Id, $"{NotificationTypes.VisitCancelled}:{visit.Id}", RoomId: visit.RoomId, LandlordId: agreement.OwnerId, At: visit.ScheduledAt),
                        cancellationToken);
                }
            }
        }
    }

    private async Task ReleaseRoomAsync(string roomId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var room = await rooms.GetAsync(roomId, cancellationToken);
            if (room is null || room.Status != RoomStatus.Rented)
            {
                return;
            }

            var released = room.Release(now) with { Version = room.Version + 1 };
            if (await rooms.ReplaceAsync(released, room.Version, cancellationToken))
            {
                return;
            }
        }

        throw new ConflictException("version_conflict");
    }

    // Busca el convenio por el código o el token. Un código de otra cuenta
    // responde igual que uno inexistente y suma un fallo al convenio.
    private async Task<Agreement> FindForStudentAsync(string studentId, string? code, string? qrToken, CancellationToken cancellationToken)
    {
        if ((code is null) == (qrToken is null))
        {
            throw new RequestValidationException("code", "Send exactly one of code or qrToken.");
        }

        var agreement = code is not null
            ? await agreements.FindByCodeHashAsync(ClosingCodes.Hash(ClosingCodes.NormalizeCode(code)), cancellationToken)
            : await agreements.FindByQrTokenHashAsync(ClosingCodes.Hash(ClosingCodes.NormalizeQrToken(qrToken!)), cancellationToken);
        if (agreement is null)
        {
            throw new NotFoundException("code", "code_invalid");
        }

        if (agreement.StudentId != studentId)
        {
            if (agreement.Status == AgreementStatus.Pending)
            {
                await agreements.RecordFailedAttemptAsync(agreement.Id, cancellationToken);
            }

            throw new NotFoundException("code", "code_invalid");
        }

        return agreement;
    }

    private Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken) =>
        notifications?.NotifyAsync(notification, cancellationToken) ?? Task.CompletedTask;

    private static ClosingCode NewCode(string backupCode, string qrToken, DateTimeOffset now) =>
        new(ClosingCodes.Hash(backupCode), ClosingCodes.Hash(qrToken), now + ClosingCode.Lifetime, null, 0);

    private async Task SaveAsync(Agreement previous, Agreement updated, CancellationToken cancellationToken)
    {
        if (!await agreements.ReplaceIfUnchangedAsync(updated, previous.UpdatedAt, cancellationToken))
        {
            throw new ConflictException("agreement_state");
        }
    }

    private async Task<IAsyncDisposable> LockRoomAsync(string roomId, CancellationToken cancellationToken) =>
        await userLock.TryAcquireAsync(RateLimitKeys.RentalRoomLock(roomId), LockTtl, cancellationToken)
            ?? throw new ConflictException("agreement_state");

    private async Task RequireLandlordAsync(string userId, CancellationToken cancellationToken)
    {
        if (await landlords.GetAsync(userId, cancellationToken) is null)
        {
            throw new ForbiddenException("landlord_required");
        }
    }

    private async Task<Agreement> LoadOwnedAsync(string ownerId, string? id, CancellationToken cancellationToken)
    {
        MessagingRules.EnsureId(id, "id");
        var agreement = await agreements.GetAsync(id!.ToLowerInvariant(), cancellationToken);
        return agreement is not null && agreement.OwnerId == ownerId ? agreement : throw new NotFoundException("agreement");
    }

    // Cuarto `available` en un edificio `verified`; si no, 409 `room_unavailable`.
    private async Task<(Room Room, Building Building)> LoadPublishedAsync(string roomId, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(roomId, cancellationToken);
        var building = room is null ? null : await buildings.GetAsync(room.BuildingId, cancellationToken);
        return room is { Status: RoomStatus.Available } && building is { Status: BuildingStatus.Verified }
            ? (room, building)
            : throw new ConflictException("room_unavailable");
    }

    private Task<LandlordAgreementResponse> ToLandlordResponseAsync(Agreement agreement, ClosingCodeResponse? code, CancellationToken cancellationToken) =>
        ToLandlordResponseAsync(agreement, null, null, code, cancellationToken);

    private async Task<LandlordAgreementResponse> ToLandlordResponseAsync(
        Agreement agreement,
        Room? room,
        Building? building,
        ClosingCodeResponse? code,
        CancellationToken cancellationToken)
    {
        room ??= await rooms.GetAsync(agreement.RoomId, cancellationToken);
        building ??= await buildings.GetAsync(agreement.BuildingId, cancellationToken);
        var profile = await profiles.GetAsync(agreement.StudentId, cancellationToken);
        string? university = null;
        if (profile?.UniversityId is { } universityId && await universities.GetByIdAsync(universityId, cancellationToken) is { } found)
        {
            university = found.Name;
        }

        return new LandlordAgreementResponse(
            agreement.Id,
            RoomSummary(agreement, room, building),
            new AgreementStudentSummary(string.IsNullOrWhiteSpace(profile?.Name) ? null : profile!.Name, university),
            TermsResponse(agreement.Terms),
            AgreementRules.Wire(agreement.Status),
            code,
            agreement.CreatedAt,
            agreement.UpdatedAt,
            agreement.ActivatedAt,
            agreement.EndedAt,
            agreement.CancelledAt);
    }

    private async Task<RentalResponse> ToRentalAsync(Agreement agreement, CancellationToken cancellationToken)
    {
        var room = await rooms.GetAsync(agreement.RoomId, cancellationToken);
        var building = await buildings.GetAsync(agreement.BuildingId, cancellationToken)
            ?? throw new NotFoundException("rental", "no_active_rental");
        var landlord = await landlords.GetAsync(agreement.OwnerId, cancellationToken);
        var next = AgreementRules.NextPaymentDate(agreement.Terms, AgreementRules.Today(timeProvider.GetUtcNow()));
        var address = building.Address;
        return new RentalResponse(
            agreement.Id,
            RoomSummary(agreement, room, building),
            BuildingSummary(building),
            TermsResponse(agreement.Terms),
            next is { } due ? new RentalNextPayment(due, agreement.Terms.MonthlyRent) : null,
            new RentalAddress(address.Street, address.Number, address.Neighborhood, address.City, address.State, address.PostalCode),
            new RentalLocation(building.Latitude, building.Longitude),
            new AgreementLandlordSummary(MessagingRules.FirstName(landlord?.FullName)),
            agreement.ConversationId,
            agreement.ActivatedAt);
    }

    private static AgreementRoomSummary RoomSummary(Agreement agreement, Room? room, Building? building) =>
        new(
            agreement.RoomId,
            agreement.BuildingId,
            building is null || room is null ? "Cuarto" : $"{building.Name} · Cuarto {room.Number}",
            building?.Address.City ?? string.Empty);

    private static AgreementBuildingSummary BuildingSummary(Building? building) =>
        new(building?.Name ?? string.Empty, building?.Rules);

    private static AgreementTermsResponse TermsResponse(AgreementTerms terms) =>
        new(terms.MonthlyRent, terms.Deposit, terms.StartDate, terms.DurationMonths, terms.EndDate, terms.PaymentDay, AgreementRules.Currency);
}

// NV-18 → NV-19: elegibilidad de reseñas por renta.
public sealed class RentalHistoryQuery(IAgreementRepository agreements) : IRentalHistoryQuery
{
    public Task<bool> HasRentedAsync(string studentId, string roomId, CancellationToken cancellationToken) =>
        agreements.HasRentedAsync(studentId, roomId, cancellationToken);
}
