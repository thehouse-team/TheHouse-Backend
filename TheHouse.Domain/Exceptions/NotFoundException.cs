namespace TheHouse.Domain.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource)
        : base("Resource not found.")
    {
        Resource = resource;
    }

    public string Resource { get; }
}
