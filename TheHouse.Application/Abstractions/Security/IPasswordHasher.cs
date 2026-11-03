namespace TheHouse.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(
        string password,
        string encodedHash);

    void VerifyDummy(string password);
}

public sealed record PasswordVerification(
    bool IsValid,
    string? ReplacementHash = null);
