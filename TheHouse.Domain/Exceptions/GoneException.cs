namespace TheHouse.Domain.Exceptions;

// NV-18: el recurso existió pero ya no se puede usar (410), por ejemplo un
// código de cierre vencido o ya usado.
public sealed class GoneException : Exception
{
    public GoneException(string reason)
        : base("The resource is no longer available.")
    {
        Reason = reason;
    }

    public string Reason { get; }
}
