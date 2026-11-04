using System.Text.Json;
using TheHouse.Application.Abstractions.Persistence;
using TheHouse.Application.Abstractions.Security;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Auth;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Domain.Exceptions;

namespace TheHouse.UnitTests;

public sealed class RegistrationValidationTests
{
    [Theory]
    [InlineData(" User+TAG@Example.COM ", "user+tag@example.com")]
    [InlineData("\tNAME@Sub.Example.org\r\n", "name@sub.example.org")]
    public void Email_is_trimmed_and_lowercased(string raw, string expected)
    {
        var normalized = EmailNormalizer.Normalize(raw);
        Assert.Equal(expected, normalized);
        Assert.True(EmailNormalizer.IsValid(normalized));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("a@localhost")]
    [InlineData(".a@example.com")]
    [InlineData("a.@example.com")]
    [InlineData("a..b@example.com")]
    [InlineData("a@example..com")]
    [InlineData("a@-example.com")]
    [InlineData("a@example-.com")]
    [InlineData("a<@example.com")]
    [InlineData("a@exam_ple.com")]
    [InlineData("Ã¡@example.com")]
    public void Invalid_email_is_rejected(string? email) =>
        Assert.False(EmailNormalizer.IsValid(EmailNormalizer.Normalize(email)));

    [Fact]
    public void Email_limits_are_applied_to_the_normalized_address()
    {
        var maximum = new string('a', 64) + "@" + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 61);
        Assert.Equal(254, maximum.Length);
        Assert.True(EmailNormalizer.IsValid(EmailNormalizer.Normalize(" " + maximum + " ")));
        Assert.False(EmailNormalizer.IsValid(maximum + "e"));
        Assert.False(EmailNormalizer.IsValid(new string('a', 65) + "@example.com"));
        Assert.False(EmailNormalizer.IsValid("a@" + new string('b', 64) + ".com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("            ")]
    [InlineData("short-value")]
    public void Invalid_password_is_rejected(string? password) =>
        Assert.Throws<RequestValidationException>(() => PasswordPolicy.EnsureValidRegistrationPassword(password));

    [Theory]
    [InlineData(12)]
    [InlineData(128)]
    public void Password_boundaries_are_accepted(int length) =>
        PasswordPolicy.EnsureValidRegistrationPassword(new string('a', length));

    [Fact]
    public void Long_password_is_rejected() => Assert.Throws<RequestValidationException>(() =>
        PasswordPolicy.EnsureValidRegistrationPassword(new string('a', 129)));

    [Theory]
    [InlineData("role")]
    [InlineData("status")]
    [InlineData("id")]
    [InlineData("passwordHash")]
    public void Dto_disallows_internal_fields(string property) => Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<RegisterRequest>("{\"email\":\"a@example.com\",\"password\":\"fixture-value-123\",\"" + property + "\":\"injected\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public async Task Use_case_normalizes_preserves_password_and_returns_only_public_fields()
    {
        var repository = new RecordingRepository();
        var hasher = new RecordingHasher();
        var service = new RegistrationService(repository, hasher, TimeProvider.System);
        const string password = "  fixture-value-123  ";
        var result = await service.RegisterAsync(new RegisterRequest { Email = " Test@Example.COM ", Password = password }, default);
        Assert.Equal(password, hasher.Received);
        Assert.Equal("test@example.com", repository.Stored?.Email);
        Assert.Equal("hash-fixture-not-a-password", repository.Stored?.PasswordHash);
        Assert.Equal(repository.Stored?.CreatedAt, repository.Stored?.UpdatedAt);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "id", "email", "createdAt" }, document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain(password, json);
        Assert.DoesNotContain("hash-fixture", json);
    }

    [Fact]
    public async Task Duplicate_index_error_maps_to_conflict()
    {
        var service = new RegistrationService(new RecordingRepository { Duplicate = true }, new RecordingHasher(), TimeProvider.System);
        await Assert.ThrowsAsync<EmailConflictException>(() => service.RegisterAsync(
            new RegisterRequest { Email = "test@example.com", Password = "fixture-value-123" }, default));
    }

    [Fact]
    public async Task Invalid_input_does_not_hash_or_insert()
    {
        var repository = new RecordingRepository();
        var hasher = new RecordingHasher();
        var service = new RegistrationService(repository, hasher, TimeProvider.System);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.RegisterAsync(
            new RegisterRequest { Email = "invalid", Password = "fixture-value-123" }, default));
        Assert.Null(repository.Stored);
        Assert.Null(hasher.Received);
    }

    [Fact]
    public async Task Cancellation_before_hashing_is_respected()
    {
        var repository = new RecordingRepository();
        var hasher = new RecordingHasher();
        var service = new RegistrationService(repository, hasher, TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RegisterAsync(
            new RegisterRequest { Email = "test@example.com", Password = "fixture-value-123" }, new CancellationToken(true)));
        Assert.Null(hasher.Received);
        Assert.Null(repository.Stored);
    }

    private sealed class RecordingRepository : IUserRepository
    {
        public User? Stored { get; private set; }
        public bool Duplicate { get; init; }

        public Task<User?> GetByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<User?>(Stored?.Email == normalizedEmail ? Stored : null);
        }

        public Task<User> CreateAsync(
            User user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Duplicate)
            {
                throw new DuplicateEmailException();
            }

            Stored = user;
            return Task.FromResult(user);
        }
    }

    private sealed class RecordingHasher : IPasswordHasher
    {
        public string? Received { get; private set; }

        public string Hash(string password)
        {
            Received = password;
            return "hash-fixture-not-a-password";
        }

        public PasswordVerification Verify(
            string password,
            string encodedHash) =>
            throw new NotSupportedException();

        public void VerifyDummy(string password)
        {
        }
    }
}
