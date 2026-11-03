namespace TheHouse.Application.Common;

public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [field] = [message],
        };
    }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
