namespace TheHouse.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string password, string encodedHash);
}

// Sin ToString generado que pueda imprimir un hash actualizado.
public sealed class PasswordVerification(bool isValid, string? replacementHash = null)
{
    public bool IsValid { get; } = isValid;
    public string? ReplacementHash { get; } = replacementHash;
}
