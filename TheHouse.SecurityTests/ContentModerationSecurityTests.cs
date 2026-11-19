using TheHouse.Application.Abstractions.Moderation;
using TheHouse.Application.Contracts.Moderation;
using TheHouse.Domain.Entities;
using TheHouse.Http;
using TheHouse.Infrastructure.Moderation;

namespace TheHouse.SecurityTests;

// NV-21 1.2.0: los rechazos nunca guardan ni devuelven el contenido; la API
// de la nube se usa sin llave; cada código se explica en español.
public sealed class ContentModerationSecurityTests
{
    [Theory]
    [InlineData(typeof(ModerationRejection))]
    [InlineData(typeof(ModerationRejectionResponse))]
    public void Rejections_never_carry_the_content(Type type)
    {
        var fields = type.GetProperties().Select(property => property.Name).ToArray();
        foreach (var forbidden in new[] { "Text", "Content", "Image", "Body", "Message", "Bytes" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }
    }

    [Fact]
    public void Rejected_exception_carries_only_field_and_categories()
    {
        var properties = typeof(ContentRejectedException).GetProperties().Select(property => property.Name).Except(typeof(Exception).GetProperties().Select(property => property.Name));
        Assert.Equal(["Field", "Categories"], properties);
    }

    [Theory]
    [InlineData("content_rejected")]
    [InlineData("moderation_unavailable")]
    public void Codes_are_explained_in_spanish(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Cloud_access_uses_the_managed_identity_and_the_ga_api()
    {
        Assert.Equal("https://cognitiveservices.azure.com", ContainerAppsIdentityTokenSource.Resource);
        Assert.Equal("2023-10-01", AzureContentSafetyClient.ApiVersion);
        Assert.Equal(TimeSpan.FromSeconds(3), AzureContentSafetyClient.Timeout);
        Assert.DoesNotContain(typeof(AzureContentSafetyClient).GetConstructors().Single().GetParameters(), parameter => parameter.Name!.Contains("key", StringComparison.OrdinalIgnoreCase));
    }
}
