namespace TheHouse.Domain.Exceptions;

// `Code` (opcional) es el código estable que recibe Mobile en el 404
// (NV-18: `code_invalid`, `no_active_rental`).
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, string? code = null)
        : base("Resource not found.")
    {
        Resource = resource;
        Code = code;
    }

    public string Resource { get; }

    public string? Code { get; }
}
