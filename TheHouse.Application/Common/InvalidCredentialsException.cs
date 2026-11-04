namespace TheHouse.Application.Common;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid credentials.")
    {
    }
}
