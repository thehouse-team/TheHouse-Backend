namespace TheHouse.Application.Common;

public sealed class EmailConflictException : Exception
{
    public EmailConflictException()
        : base("The email is already registered.")
    {
    }
}
