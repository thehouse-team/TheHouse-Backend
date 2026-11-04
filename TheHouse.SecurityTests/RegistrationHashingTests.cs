using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using TheHouse.Infrastructure.Configuration;
using TheHouse.Infrastructure.Security;

namespace TheHouse.SecurityTests;

public sealed class RegistrationHashingTests
{
    [Fact]
    public void Default_work_factor_and_salt_subkey_format_match_spec()
    {
        var hasher = Create();
        var encoded = hasher.Hash("fixture-value-123");
        var parts = encoded.Split('$');
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("210000", parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        Assert.Equal(16, salt.Length);
        var subkey = Convert.FromBase64String(parts[3]);
        Assert.Equal(32, subkey.Length);
        Assert.Equal(Rfc2898DeriveBytes.Pbkdf2("fixture-value-123", salt, 210000, HashAlgorithmName.SHA256, 32), subkey);
        Assert.DoesNotContain("fixture-value-123", encoded);
        Assert.True(hasher.Verify("fixture-value-123", encoded).IsValid);
        Assert.False(hasher.Verify("different-value-123", encoded).IsValid);
    }

    [Fact]
    public void Equal_passwords_have_independent_salts()
    {
        var hasher = Create();
        var first = hasher.Hash("fixture-value-123");
        var second = hasher.Hash("fixture-value-123");
        Assert.NotEqual(first.Split('$')[2], second.Split('$')[2]);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Password_is_not_trimmed_or_normalized()
    {
        var hasher = Create();
        var encoded = hasher.Hash("  fixture-value-123  ");
        Assert.True(hasher.Verify("  fixture-value-123  ", encoded).IsValid);
        Assert.False(hasher.Verify("fixture-value-123", encoded).IsValid);
    }

    [Fact]
    public void Verification_uses_stored_iterations_and_returns_rehash_only_after_success()
    {
        var oldHash = Create().Hash("fixture-value-123");
        var current = Create(210001);
        var verified = current.Verify("fixture-value-123", oldHash);
        Assert.True(verified.IsValid);
        Assert.StartsWith("pbkdf2-sha256$210001$", verified.ReplacementHash);
        Assert.True(current.Verify("fixture-value-123", verified.ReplacementHash!).IsValid);
        Assert.Null(current.Verify("different-value-123", oldHash).ReplacementHash);
        var same = Create().Verify("fixture-value-123", oldHash);
        Assert.True(same.IsValid);
        Assert.Null(same.ReplacementHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(209999)]
    [InlineData(2000001)]
    public void Unsafe_work_factor_is_rejected(int iterations) =>
        Assert.Throws<ValidationException>(() => Create(iterations));

    [Theory]
    [InlineData("")]
    [InlineData("other$210000$AA==$AA==")]
    [InlineData("pbkdf2-sha256$1$AA==$AA==")]
    [InlineData("pbkdf2-sha256$2147483647$AA==$AA==")]
    [InlineData("pbkdf2-sha256$210000$invalid$invalid")]
    [InlineData("pbkdf2-sha256$210000$AA==$AA==")]
    public void Corrupt_hashes_fail_closed_without_expensive_unbounded_derivation(string encoded) =>
        Assert.False(Create().Verify("fixture-value-123", encoded).IsValid);

    [Fact]
    public void Iteration_encoding_is_culture_invariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.StartsWith("pbkdf2-sha256$210000$", Create().Hash("fixture-value-123"));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static Pbkdf2PasswordHasher Create(int iterations = PasswordHashingOptions.MinimumIterations) =>
        new(new PasswordHashingOptions { Iterations = iterations });
}
