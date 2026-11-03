namespace TheHouse.Application.Common;

public static class PasswordPolicy
{
    public const int RegistrationMinimumLength = 12;
    public const int MaximumLength = 128;

    public static void EnsureValidRegistrationPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new RequestValidationException("password", "password is required.");
        }

        if (password.Length is < RegistrationMinimumLength or > MaximumLength)
        {
            throw new RequestValidationException(
                "password",
                $"password must be between {RegistrationMinimumLength} and {MaximumLength} characters.");
        }
    }

}
