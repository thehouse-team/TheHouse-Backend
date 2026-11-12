using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Notifications;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;

namespace TheHouse.SecurityTests;

// NV-20: asignación masiva bloqueada, textos de todas las plantillas sin datos
// sensibles y respuestas sin ids de usuario (RNF-02).
public sealed class NotificationSecurityTests
{
    [Fact]
    public void Read_all_rejects_unknown_fields()
    {
        var attribute = typeof(ReadAllNotificationsRequest).GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadAllNotificationsRequest>(
            "{\"category\":\"requests\",\"userId\":\"x\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Templates_only_use_the_given_name_and_place()
    {
        // Las plantillas no reciben dirección, teléfono, correo ni texto del
        // chat: solo nombre, lugar y fecha. Ningún texto puede filtrar más.
        foreach (var type in NotificationTypes.Catalog.Keys)
        {
            var (title, body) = NotificationTemplates.Render(type, "Ana", "Casa Azul · Cuarto 101", DateTimeOffset.UtcNow);
            var text = title + " " + body;
            Assert.DoesNotContain("@", text);
            Assert.DoesNotContain("+52", text);
            Assert.DoesNotMatch(@"[0-9a-f]{24}", text);
        }
    }

    [Fact]
    public void Responses_never_expose_user_ids_or_dedupe_keys()
    {
        var fields = typeof(NotificationResponse).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("UserId", fields);
        Assert.DoesNotContain("DedupeKey", fields);
        Assert.Equal(new[] { "Kind", "Id" }, typeof(NotificationTarget).GetProperties().Select(property => property.Name));
    }
}
