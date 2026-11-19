using System.Reflection;
using System.Text.Json.Serialization;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;
using TheHouse.Configuration;
using TheHouse.Domain.Entities;
using TheHouse.Http;
using TheHouse.Infrastructure.Notifications;

namespace TheHouse.SecurityTests;

// NV-20 1.2.0: el token de FCM nunca sale; el push no lleva contenido
// privado; la credencial de Firebase solo se acepta para Google.
public sealed class PushSecurityTests
{
    [Theory]
    [InlineData(typeof(PushDeviceResponse))]
    [InlineData(typeof(PushDeviceListResponse))]
    public void Device_responses_never_carry_the_token_or_the_user(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();
        foreach (var forbidden in new[] { "FcmToken", "Token", "UserId", "Locale" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Device_body_rejects_unknown_fields()
    {
        var attribute = typeof(PushDeviceRequest).GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
    }

    [Fact]
    public void Push_text_is_the_inbox_text_without_message_content()
    {
        var (title, body) = NotificationTemplates.Render(NotificationTypes.MessageReceived, "Ana", "Casa Verde 101", null);
        Assert.Equal("Mensaje nuevo", title);
        Assert.Equal("Ana te escribió sobre Casa Verde 101.", body);
        Assert.DoesNotContain("Message", typeof(TheHouse.Application.Abstractions.Notifications.PushMessage).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void Credential_and_limits_are_bounded()
    {
        Assert.Equal("FIREBASE_SERVICE_ACCOUNT", PushConfiguration.SecretName);
        Assert.Equal("https://oauth2.googleapis.com/token", FirebaseServiceAccount.TokenUri);
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", FcmPushSender.Scope);
        Assert.Null(FirebaseServiceAccount.Parse("{\"project_id\":\"p\",\"client_email\":\"e\",\"private_key\":\"x\",\"token_uri\":\"http://169.254.169.254/token\"}"));
        Assert.Equal(5, PushRules.MaximumDevices);
        Assert.Equal(30, new UserRateLimitOptions().PushDeviceRegistrationsPerHour);
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor("installation_conflict")));
        Assert.StartsWith("/api/v1/users/me/", PushDevicesEndpoint.BasePath, StringComparison.Ordinal);
    }
}
