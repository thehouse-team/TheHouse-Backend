namespace TheHouse.Domain.Entities;

// NV-22: hábitos del perfil de compañero (prototipo «Buscar compañero»).
public sealed record RoommateHabits(
    string Schedule,
    string Cleaning,
    string Noise,
    string Smoking,
    string Pets,
    string Social)
{
    public int SharedWith(RoommateHabits other) =>
        (Schedule == other.Schedule ? 1 : 0) +
        (Cleaning == other.Cleaning ? 1 : 0) +
        (Noise == other.Noise ? 1 : 0) +
        (Smoking == other.Smoking ? 1 : 0) +
        (Pets == other.Pets ? 1 : 0) +
        (Social == other.Social ? 1 : 0);
}

// NV-22: perfil de compañero voluntario, uno por usuario. Aparece en la
// lista mientras está visible y vigente (60 días desde su último cambio).
public sealed record RoommateProfile
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    public string Id { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string UniversityId { get; init; } = string.Empty;

    public bool Visible { get; init; }

    public int BudgetMin { get; init; }

    public int BudgetMax { get; init; }

    public string Zone { get; init; } = string.Empty;

    // Mes de llegada en formato `YYYY-MM` (se compara como texto).
    public string MoveIn { get; init; } = string.Empty;

    public int DurationMonths { get; init; }

    public bool HasPlace { get; init; }

    public RoommateHabits Habits { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public string? About { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsListed(DateTimeOffset now) => Visible && ExpiresAt > now;

    // Coincidencias: hábitos iguales (0–6) más 1 si los presupuestos se cruzan.
    public int MatchesWith(RoommateProfile other) =>
        Habits.SharedWith(other.Habits) + (BudgetMin <= other.BudgetMax && other.BudgetMin <= BudgetMax ? 1 : 0);
}

// NV-16/NV-22: tipo de conversación. Las de renta tienen solicitud y cuarto;
// las de compañero son entre dos estudiantes, una por pareja.
public static class ConversationKinds
{
    public const string Rental = "rental";

    public const string Roommate = "roommate";

    // Llave única de la pareja, guardada como `requestId` interno para que el
    // índice único existente impida conversaciones duplicadas.
    public static string RoommateKey(string first, string second) =>
        "roommate:" + string.Join(":", new[] { first, second }.Order(StringComparer.Ordinal));
}
