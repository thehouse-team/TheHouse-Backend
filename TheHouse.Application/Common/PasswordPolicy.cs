namespace TheHouse.Application.Common;

public static class PasswordPolicy
{
    public const int RegistrationMinimumLength = 12;
    public const int MaximumLength = 128;

    public static void EnsureValidRegistrationPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < RegistrationMinimumLength ||
            password.Length > MaximumLength)
        {
            throw new RequestValidationException(
                "password",
                $"password must contain between {RegistrationMinimumLength} and {MaximumLength} characters.");
        }
    }

    public static void EnsureValidLoginPassword(string? password)
    {
        if (password is null ||
            password.Length < 1 ||
            password.Length > MaximumLength)
        {
            throw new RequestValidationException(
                "password",
                $"password must contain between 1 and {MaximumLength} characters.");
        }
    }
}
