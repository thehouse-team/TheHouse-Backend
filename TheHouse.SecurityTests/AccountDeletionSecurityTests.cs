using System.Reflection;
using System.Text.Json.Serialization;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Users;
using TheHouse.Application.UseCases;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-10 1.2.0: la eliminación pide reautenticación, tiene límites, no expone
// datos de la cuenta y explica cada código en español.
public sealed class AccountDeletionSecurityTests
{
    [Theory]
    [InlineData(typeof(DeletionPreviewResponse))]
    [InlineData(typeof(DeletionEffectsResponse))]
    [InlineData(typeof(LegalResponse))]
    [InlineData(typeof(LegalAcceptanceResponse))]
    [InlineData(typeof(MeResponse))]
    public void Account_responses_never_carry_secrets(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();
        foreach (var forbidden in new[] { "Password", "PasswordHash", "RefreshToken", "SessionId", "Phone", "UserId" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Theory]
    [InlineData(typeof(AccountDeletionRequest))]
    [InlineData(typeof(LegalAcceptanceRequest))]
    public void Request_bodies_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
    }

    [Fact]
    public void Deletion_is_rate_limited_and_requires_the_typed_confirmation()
    {
        var limits = new UserRateLimitOptions();
        Assert.Equal(5, limits.AccountDeletionsPerHour);
        Assert.Equal(30, limits.AccountPerWindow);
        Assert.NotEqual(RateLimitKeys.AccountUser("u"), RateLimitKeys.AccountDeletionUser("u"));
        Assert.DoesNotContain("u", RateLimitKeys.AccountDeletionUser("u").Split(':')[^1], StringComparison.Ordinal);
        Assert.Equal("ELIMINAR", AccountDeletionRules.Confirmation);
        Assert.StartsWith("/api/v1/users/me/", AccountEndpoint.DeletionPath, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("password_mismatch")]
    [InlineData("deletion_blocked")]
    [InlineData("deletion_incomplete")]
    [InlineData("legal_version_outdated")]
    public void Codes_are_explained_in_spanish(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Session_has_no_absolute_cap_but_expires_when_idle()
    {
        var options = new RefreshTokenOptions();
        Assert.Equal(30, options.SessionLifetimeDays);
        Assert.Equal(options.TokenLifetimeDays, options.SessionLifetimeDays);
        Assert.True(options.IsValid());
    }
}
