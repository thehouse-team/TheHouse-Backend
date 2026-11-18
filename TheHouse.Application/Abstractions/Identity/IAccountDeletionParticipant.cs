namespace TheHouse.Application.Abstractions.Identity;

// NV-10 1.2.0: cada módulo implementa este puerto para sus propios datos.
// Identity no conoce a los demás módulos (sin dependencias circulares,
// constitution §6): solo recorre los participantes registrados.
public interface IAccountDeletionParticipant
{
    // Bloqueos (por ejemplo, una renta activa) y efectos que se muestran
    // antes de confirmar. Nunca cambia datos.
    Task<AccountDeletionCheck> CheckAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken);

    // Borra, cancela o anonimiza los datos del usuario. Debe ser idempotente:
    // si la eliminación falla a mitad, repetirla termina el trabajo.
    Task EraseAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed record AccountDeletionCheck(
    IReadOnlyList<AccountDeletionBlocker> Blockers,
    IReadOnlyDictionary<string, int> Effects)
{
    public static AccountDeletionCheck None { get; } =
        new([], new Dictionary<string, int>(StringComparer.Ordinal));

    public static AccountDeletionCheck Of(params (string Effect, int Count)[] effects) =>
        new([], effects.ToDictionary(effect => effect.Effect, effect => effect.Count, StringComparer.Ordinal));
}

public sealed record AccountDeletionBlocker(string Code, int Count);

// Nombres estables de los efectos y bloqueos del contrato §6.1.
public static class AccountDeletionEffects
{
    public const string Buildings = "buildings";
    public const string Rooms = "rooms";
    public const string UpcomingVisits = "upcomingVisits";
    public const string OpenRequests = "openRequests";
    public const string PendingAgreements = "pendingAgreements";
    public const string Conversations = "conversations";
    public const string Reviews = "reviews";
    public const string Favorites = "favorites";
    public const string RoommateProfile = "roommateProfile";

    public const string ActiveRental = "active_rental";
    public const string StaffAccount = "staff_account";
}
