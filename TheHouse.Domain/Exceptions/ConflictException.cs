namespace TheHouse.Domain.Exceptions;

// Conflicto con el estado actual del recurso (409). `Reason` es un código
// interno para pruebas y diagnóstico; nunca se expone con datos del usuario.
public sealed class ConflictException : Exception
{
    public ConflictException(string reason)
        : base("Conflict.")
    {
        Reason = reason;
    }

    public string Reason { get; }
}
