using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Infrastructure.Configuration;

namespace TheHouse.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Algorithm = "pbkdf2-sha256";
    private readonly int _iterations;

    public Pbkdf2PasswordHasher(PasswordHashingOptions options)
    {
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        _iterations = options.Iterations;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length is < 1 or > 128) throw new ArgumentException("Invalid password length.");
        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        try
        {
            return $"{Algorithm}${_iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    public PasswordVerification Verify(string password, string encodedHash)
    {
        if (password is null || password.Length is < 1 or > 128 || encodedHash is null || encodedHash.Length > 256)
            return new(false);
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
        {
            return new(false);
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations < PasswordHashingOptions.MinimumIterations
            || iterations > PasswordHashingOptions.MaximumIterations)
        {
            return new(false);
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expected.Length != KeySize)
            {
                return new(false);
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            try
            {
                var valid = CryptographicOperations.FixedTimeEquals(actual, expected);
                return new(valid, valid && iterations != _iterations ? Hash(password) : null);
            }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
        catch (FormatException)
        {
            return new(false);
        }
    }

}
