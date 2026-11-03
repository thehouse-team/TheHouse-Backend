using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int SubkeyBytes = 32;

    private readonly int iterations;
    private readonly string dummyHash;

    public Pbkdf2PasswordHasher(PasswordHashingOptions options)
    {
        if (options.Iterations < PasswordHashingOptions.MinimumIterations ||
            options.Iterations > PasswordHashingOptions.MaximumIterations)
        {
            throw new ValidationException("Invalid password hashing configuration.");
        }

        iterations = options.Iterations;
        dummyHash = HashInternal("TheHouse-Dummy-Credential-Never-Valid");
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return HashInternal(password);
    }

    public PasswordVerification Verify(
        string password,
        string encodedHash)
    {
        if (password is null || string.IsNullOrWhiteSpace(encodedHash))
        {
            return new PasswordVerification(false);
        }

        try
        {
            var parts = encodedHash.Split('$');

            if (parts.Length != 4 ||
                !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
            {
                return new PasswordVerification(false);
            }

            if (!int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var storedIterations) ||
                storedIterations < PasswordHashingOptions.MinimumIterations ||
                storedIterations > PasswordHashingOptions.MaximumIterations)
            {
                return new PasswordVerification(false);
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            if (salt.Length != SaltBytes || expected.Length != SubkeyBytes)
            {
                return new PasswordVerification(false);
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                storedIterations,
                HashAlgorithmName.SHA256,
                SubkeyBytes);

            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            {
                return new PasswordVerification(false);
            }

            var replacement = storedIterations == iterations
                ? null
                : HashInternal(password);

            return new PasswordVerification(true, replacement);
        }
        catch (FormatException)
        {
            return new PasswordVerification(false);
        }
        catch (CryptographicException)
        {
            return new PasswordVerification(false);
        }
    }

    public void VerifyDummy(string password)
    {
        _ = Verify(password, dummyHash);
    }

    private string HashInternal(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);

        var subkey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            SubkeyBytes);

        return string.Join(
            '$',
            Algorithm,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }
}
