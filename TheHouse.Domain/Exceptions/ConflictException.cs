namespace TheHouse.Domain.Exceptions;

// Conflicto con el estado actual del recurso (409). `Reason` es un código
// estable (M-BE-061); `Detail` opcional describe pasos pendientes sin datos
// del usuario (por ejemplo, `publication_incomplete`).
public sealed class ConflictException : Exception
{
    public ConflictException(string reason, string? detail = null)
        : base("Conflict.")
    {
        Reason = reason;
        Detail = detail;
    }

    public string Reason { get; }

    public string? Detail { get; }
}
