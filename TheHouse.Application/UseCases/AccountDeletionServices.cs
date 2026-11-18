using TheHouse.Application.Abstractions.Identity;
using TheHouse.Application.Abstractions.Notifications;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.Application.UseCases;

// NV-10 1.2.0: reglas compartidas de la eliminación de cuenta.
public static class AccountDeletionRules
{
    public const string Confirmation = "ELIMINAR";

    // Motivo de las cancelaciones y de la revocación de sesiones.
    public const string Reason = "account_deleted";

    // Lo que ve la otra persona en conversaciones, convenios y reseñas.
    public const string DisplayName = "Usuario eliminado";

    public const int PageSize = 100;

    // Horizonte para cancelar visitas futuras en los cuartos del arrendador.
    public static readonly TimeSpan VisitHorizon = TimeSpan.FromDays(180);

    // Recorre todas las páginas de una consulta paginada (1, 2, …).
    public static async Task<List<T>> AllAsync<T>(
        Func<int, Task<(IReadOnlyList<T> Items, long TotalItems)>> page)
    {
        var all = new List<T>();
        for (var number = 1; ; number++)
        {
            var (items, total) = await page(number);
            all.AddRange(items);
            if (items.Count == 0 || all.Count >= total)
            {
                return all;
            }
        }
    }
}

public sealed class AccountDeletionBlockedException(IReadOnlyList<AccountDeletionBlocker> blockers) : Exception("Account deletion blocked.")
{
    public IReadOnlyList<AccountDeletionBlocker> Blockers { get; } = blockers;
}

// Un paso falló: la sesión sigue viva y repetir la llamada termina el trabajo.
public sealed class AccountDeletionIncompleteException() : Exception("Account deletion incomplete.");

// Vista previa y eliminación. Identity solo conoce el puerto
// IAccountDeletionParticipant; cada módulo borra sus propios datos.
public sealed class AccountDeletionService(
    IUserRepository users,
    IUserAccountRepository accounts,
    IPasswordHasher passwordHasher,
    IEnumerable<IAccountDeletionParticipant> participants,
    IRefreshTokenStore refreshTokens,
    SessionService sessions,
    TimeProvider timeProvider)
{
    public async Task<DeletionPreviewResponse> PreviewAsync(string userId, CancellationToken cancellationToken)
    {
        _ = await users.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidCredentialsException();
        var (blockers, effects) = await CheckAsync(userId, timeProvider.GetUtcNow(), cancellationToken);
        return new DeletionPreviewResponse(
            blockers.Count == 0,
            [.. blockers.Select(blocker => new DeletionBlockerResponse(blocker.Code, blocker.Count))],
            new DeletionEffectsResponse(
                Effect(effects, AccountDeletionEffects.Buildings),
                Effect(effects, AccountDeletionEffects.Rooms),
                Effect(effects, AccountDeletionEffects.UpcomingVisits),
                Effect(effects, AccountDeletionEffects.OpenRequests),
                Effect(effects, AccountDeletionEffects.PendingAgreements),
                Effect(effects, AccountDeletionEffects.Conversations),
                Effect(effects, AccountDeletionEffects.Reviews),
                Effect(effects, AccountDeletionEffects.Favorites),
                Effect(effects, AccountDeletionEffects.RoommateProfile) > 0));
    }

    public async Task DeleteAsync(string userId, AccountDeletionRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request?.Password) || request.Password.Length > 128)
        {
            throw new RequestValidationException("password", "password is required.");
        }

        if (!string.Equals(request.Confirmation, AccountDeletionRules.Confirmation, StringComparison.Ordinal))
        {
            throw new RequestValidationException("confirmation", "confirmation must be ELIMINAR.");
        }

        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new InvalidCredentialsException();

        // Reautenticación: un teléfono desbloqueado o un token robado no basta.
        if (!passwordHasher.Verify(request.Password, user.PasswordHash).IsValid)
        {
            throw new ForbiddenException("password_mismatch");
        }

        var now = timeProvider.GetUtcNow();
        var (blockers, _) = await CheckAsync(userId, now, cancellationToken);
        if (blockers.Count > 0)
        {
            throw new AccountDeletionBlockedException(blockers);
        }

        try
        {
            // Primero los módulos, con la sesión viva para poder reintentar.
            foreach (var participant in participants)
            {
                await participant.EraseAsync(userId, now, cancellationToken);
            }

            // Al final: todas las sesiones (access y refresh) y la cuenta.
            foreach (var sessionId in await refreshTokens.ListActiveSessionIdsAsync(userId, now, cancellationToken))
            {
                await sessions.RevokeSessionAsync(sessionId, AccountDeletionRules.Reason, cancellationToken);
            }

            await accounts.DeleteAsync(userId, now, cancellationToken);
        }
        catch (DependencyUnavailableException)
        {
            throw new AccountDeletionIncompleteException();
        }
    }

    private async Task<(List<AccountDeletionBlocker> Blockers, Dictionary<string, int> Effects)> CheckAsync(
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var blockers = new List<AccountDeletionBlocker>();
        var effects = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var participant in participants)
        {
            var check = await participant.CheckAsync(userId, now, cancellationToken);
            blockers.AddRange(check.Blockers);
            foreach (var (name, count) in check.Effects)
            {
                effects[name] = effects.GetValueOrDefault(name) + count;
            }
        }

        return (blockers, effects);
    }

    private static int Effect(Dictionary<string, int> effects, string name) => effects.GetValueOrDefault(name);
}

// --- Participantes por módulo ------------------------------------------------

// Profile (NV-11): el nombre queda como «Usuario eliminado» (sin universidad)
// para que la otra persona lo vea así; las preferencias se borran.
public sealed class ProfileDeletionParticipant(IProfileRepository profiles, IPreferencesRepository preferences) : IAccountDeletionParticipant
{
    public Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(AccountDeletionCheck.None);

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await profiles.AnonymizeAsync(userId, AccountDeletionRules.DisplayName, now, cancellationToken);
        await preferences.DeleteAsync(userId, cancellationToken);
    }
}

// Listings y Media (NV-12/NV-13): edificios `deleted` (fuera de búsqueda y
// detalle), fotos y documentos borrados con sus archivos, perfil de
// arrendador sin nombre real ni teléfono.
public sealed class ListingsDeletionParticipant(
    ILandlordProfileRepository landlords,
    IBuildingRepository buildings,
    IRoomRepository rooms,
    IMediaRepository media) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var owned = (await buildings.ListAllByOwnerAsync(userId, cancellationToken))
            .Where(building => building.Status != BuildingStatus.Deleted)
            .Select(building => building.Id)
            .ToHashSet(StringComparer.Ordinal);
        var ownedRooms = (await rooms.ListAllByOwnerAsync(userId, cancellationToken)).Count(room => owned.Contains(room.BuildingId));
        return AccountDeletionCheck.Of((AccountDeletionEffects.Buildings, owned.Count), (AccountDeletionEffects.Rooms, ownedRooms));
    }

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var building in await buildings.ListAllByOwnerAsync(userId, cancellationToken))
        {
            await media.DeleteByBuildingAsync(building.Id, cancellationToken);
            if (building.Status != BuildingStatus.Deleted)
            {
                var deleted = building with { Status = BuildingStatus.Deleted, UpdatedAt = now, Version = building.Version + 1 };
                if (!await buildings.ReplaceAsync(deleted, building.Version, cancellationToken))
                {
                    // Otro cambio ganó la carrera: repetir la eliminación lo retira.
                    throw new DependencyUnavailableException();
                }
            }
        }

        if (await landlords.GetAsync(userId, cancellationToken) is not null)
        {
            await landlords.AnonymizeAsync(userId, AccountDeletionRules.DisplayName, now, cancellationToken);
        }
    }
}

// Favorites (NV-15).
public sealed class FavoritesDeletionParticipant(IFavoriteRepository favorites) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        AccountDeletionCheck.Of((AccountDeletionEffects.Favorites, (int)await favorites.CountAsync(userId, cancellationToken)));

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var favorite in await favorites.ListAllAsync(userId, cancellationToken))
        {
            await favorites.RemoveAsync(userId, favorite.RoomId, favorite.LegacyPropertyId, cancellationToken);
        }
    }
}

// Messaging (NV-16 y chat de NV-22): solicitudes abiertas canceladas o
// rechazadas con aviso; conversaciones cerradas (solo lectura).
public sealed class MessagingDeletionParticipant(
    IRentalRequestRepository requests,
    IConversationRepository conversations,
    INotificationSink? notifications = null) : IAccountDeletionParticipant
{
    private static readonly RentalRequestStatus[] Open = [RentalRequestStatus.Pending, RentalRequestStatus.Accepted];

    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = 0L;
        foreach (var status in Open)
        {
            open += (await requests.ListByStudentAsync(userId, status, 1, 1, cancellationToken)).TotalItems;
            open += (await requests.ListByOwnerAsync(userId, status, null, 1, 1, cancellationToken)).TotalItems;
        }

        var (_, total) = await conversations.ListForUserAsync(userId, 1, 1, cancellationToken);
        return AccountDeletionCheck.Of((AccountDeletionEffects.OpenRequests, (int)open), (AccountDeletionEffects.Conversations, (int)total));
    }

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var status in Open)
        {
            var sent = await AccountDeletionRules.AllAsync(page => requests.ListByStudentAsync(userId, status, page, AccountDeletionRules.PageSize, cancellationToken));
            foreach (var request in sent)
            {
                var cancelled = request with { Status = RentalRequestStatus.Cancelled, UpdatedAt = now };
                if (await requests.UpdateStatusAsync(cancelled, status, cancellationToken))
                {
                    await NotifyAsync(new NotificationEvent(NotificationTypes.RequestCancelled, request.OwnerId, request.Id, $"{NotificationTypes.RequestCancelled}:{request.Id}", RoomId: request.RoomId), cancellationToken);
                }
            }

            var received = await AccountDeletionRules.AllAsync(page => requests.ListByOwnerAsync(userId, status, null, page, AccountDeletionRules.PageSize, cancellationToken));
            foreach (var request in received)
            {
                var rejected = request with
                {
                    Status = RentalRequestStatus.Rejected,
                    Rejection = new RequestRejection(AccountDeletionRules.Reason, null),
                    UpdatedAt = now,
                };
                if (await requests.UpdateStatusAsync(rejected, status, cancellationToken))
                {
                    await NotifyAsync(new NotificationEvent(NotificationTypes.RequestRejected, request.StudentId, request.Id, $"{NotificationTypes.RequestRejected}:{request.Id}", RoomId: request.RoomId), cancellationToken);
                }
            }
        }

        var all = await AccountDeletionRules.AllAsync(page => conversations.ListForUserAsync(userId, page, AccountDeletionRules.PageSize, cancellationToken));
        foreach (var conversation in all)
        {
            await conversations.CloseAsync(conversation.Id, cancellationToken);
        }
    }

    private Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken) =>
        notifications?.NotifyAsync(notification, cancellationToken) ?? Task.CompletedTask;
}

// Visits (NV-17): visitas futuras propias y en sus cuartos, canceladas con aviso.
public sealed class VisitsDeletionParticipant(
    IVisitRepository visits,
    IRoomRepository rooms,
    INotificationSink? notifications = null) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        AccountDeletionCheck.Of((AccountDeletionEffects.UpcomingVisits, (await OwnAsync(userId, now, cancellationToken)).Count + (await ReceivedAsync(userId, now, cancellationToken)).Count));

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var visit in await OwnAsync(userId, now, cancellationToken))
        {
            if (await visits.ReplaceIfUnchangedAsync(visit.Cancel(AccountDeletionRules.Reason, now), visit.UpdatedAt, cancellationToken) &&
                await rooms.GetAsync(visit.RoomId, cancellationToken) is { } room)
            {
                await NotifyAsync(new NotificationEvent(NotificationTypes.VisitCancelled, room.OwnerId, visit.Id, $"{NotificationTypes.VisitCancelled}:{visit.Id}", RoomId: visit.RoomId, StudentId: visit.UserId, At: visit.ScheduledAt), cancellationToken);
            }
        }

        foreach (var visit in await ReceivedAsync(userId, now, cancellationToken))
        {
            if (await visits.ReplaceIfUnchangedAsync(visit.CancelByLandlord(AccountDeletionRules.Reason, null, now), visit.UpdatedAt, cancellationToken))
            {
                await NotifyAsync(new NotificationEvent(NotificationTypes.VisitCancelled, visit.UserId, visit.Id, $"{NotificationTypes.VisitCancelled}:{visit.Id}", RoomId: visit.RoomId, LandlordId: userId, At: visit.ScheduledAt), cancellationToken);
            }
        }
    }

    private async Task<List<Visit>> OwnAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        [.. (await visits.ListActiveAsync(userId, now, cancellationToken)).Where(visit => visit.IsActive(now))];

    private async Task<List<Visit>> ReceivedAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var roomIds = (await rooms.ListAllByOwnerAsync(userId, cancellationToken)).Select(room => room.Id).ToArray();
        if (roomIds.Length == 0)
        {
            return [];
        }

        var found = new List<Visit>();
        foreach (var status in new[] { VisitStatus.Requested, VisitStatus.Confirmed })
        {
            found.AddRange(await AccountDeletionRules.AllAsync(page =>
                visits.ListForRoomsAsync(roomIds, now, now.Add(AccountDeletionRules.VisitHorizon), status, page, AccountDeletionRules.PageSize, cancellationToken)));
        }

        return [.. found.Where(visit => visit.UserId != userId && visit.IsActive(now))];
    }

    private Task NotifyAsync(NotificationEvent notification, CancellationToken cancellationToken) =>
        notifications?.NotifyAsync(notification, cancellationToken) ?? Task.CompletedTask;
}

// Rentals (NV-18): una renta activa impide eliminar; los convenios
// pendientes se cancelan; los terminados se conservan para la otra persona.
public sealed class RentalsDeletionParticipant(IAgreementRepository agreements) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = await agreements.ListOpenForUserAsync(userId, cancellationToken);
        var active = open.Count(agreement => agreement.Status == AgreementStatus.Active);
        var pending = open.Count(agreement => agreement.Status == AgreementStatus.Pending);
        return new AccountDeletionCheck(
            active > 0 ? [new AccountDeletionBlocker(AccountDeletionEffects.ActiveRental, active)] : [],
            new Dictionary<string, int>(StringComparer.Ordinal) { [AccountDeletionEffects.PendingAgreements] = pending });
    }

    public async Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var agreement in (await agreements.ListOpenForUserAsync(userId, cancellationToken)).Where(agreement => agreement.Status == AgreementStatus.Pending))
        {
            await agreements.ReplaceIfUnchangedAsync(agreement.Cancel(now), agreement.UpdatedAt, cancellationToken);
        }
    }
}

// Reviews (NV-19): las reseñas escritas siguen visibles; el autor se ve
// como «Usuario eliminado» porque su perfil queda anonimizado.
public sealed class ReviewsDeletionParticipant(IReviewRepository reviews) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        AccountDeletionCheck.Of((AccountDeletionEffects.Reviews, (int)(await reviews.ListByAuthorAsync(userId, 1, 1, cancellationToken)).TotalItems));

    public Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Notifications (NV-20): la bandeja se borra.
public sealed class NotificationsDeletionParticipant(INotificationRepository notifications) : IAccountDeletionParticipant
{
    public Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(AccountDeletionCheck.None);

    public Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        notifications.DeleteAllAsync(userId, cancellationToken);
}

// Moderation (NV-21): staff primero pierde el rol; reportes y auditoría
// conservan solo el id interno.
public sealed class ModerationDeletionParticipant(IStaffRoleRepository staff) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await staff.GetAsync(userId, cancellationToken) is { Roles.Count: > 0 }
            ? new AccountDeletionCheck([new AccountDeletionBlocker(AccountDeletionEffects.StaffAccount, 1)], new Dictionary<string, int>(StringComparer.Ordinal))
            : AccountDeletionCheck.None;

    public Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Roommates (NV-22): el perfil de compañero se borra.
public sealed class RoommatesDeletionParticipant(IRoommateProfileRepository roommates) : IAccountDeletionParticipant
{
    public async Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        AccountDeletionCheck.Of((AccountDeletionEffects.RoommateProfile, await roommates.GetByUserAsync(userId, cancellationToken) is null ? 0 : 1));

    public Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        roommates.DeleteByUserAsync(userId, cancellationToken);
}

// Analytics (NV-25): las vistas de sus cuartos se borran.
public sealed class AnalyticsDeletionParticipant(IRoomViewRepository views) : IAccountDeletionParticipant
{
    public Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(AccountDeletionCheck.None);

    public Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        views.DeleteByOwnerAsync(userId, cancellationToken);
}
