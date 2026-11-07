namespace TheHouse.Domain.Exceptions;

// Falta un rol para la operación (403), por ejemplo `landlord_required`.
// Un recurso ajeno no usa esta excepción: responde 404 (BOLA).
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string reason)
        : base("Forbidden.")
    {
        Reason = reason;
    }

    public string Reason { get; }
}
