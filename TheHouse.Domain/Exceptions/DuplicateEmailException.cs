namespace TheHouse.Domain.Exceptions;

public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("A user with the same email already exists.")
    {
    }
}
