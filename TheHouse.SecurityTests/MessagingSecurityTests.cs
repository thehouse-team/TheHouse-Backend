using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.Messaging;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-16: privacidad de las llaves de Redis, asignación masiva bloqueada y
// códigos de conflicto con mensaje en español.
public sealed class MessagingSecurityTests
{
    private const string UserId = "665f1c2a9b1e4a00000a11ce";
    private const string ConversationId = "6a0000000000000000000001";

    [Fact]
    public void Redis_keys_never_contain_raw_user_or_conversation_ids()
    {
        var keys = new[]
        {
            RateLimitKeys.MessagingUser(UserId),
            RateLimitKeys.RequestCreatesUser(UserId),
            RateLimitKeys.MessageSendsUser(UserId),
            RateLimitKeys.MessagePolls(UserId, ConversationId),
            RateLimitKeys.MessagingLock(UserId),
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key =>
        {
            Assert.DoesNotContain(UserId, key, StringComparison.Ordinal);
            Assert.DoesNotContain(ConversationId, key, StringComparison.Ordinal);
            Assert.Matches("^thehouse:u2:messaging:[a-z]+:v1:[0-9a-f]{64}$", key);
        });
    }

    [Theory]
    [InlineData(typeof(CreateRentalRequest))]
    [InlineData(typeof(RejectRentalRequest))]
    [InlineData(typeof(SendMessageRequest))]
    [InlineData(typeof(MarkReadRequest))]
    public void Inputs_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute!.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"senderId\":\"x\",\"status\":\"accepted\"}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("own_room")]
    [InlineData("active_request_exists")]
    [InlineData("active_requests_limit")]
    [InlineData("request_in_progress")]
    [InlineData("request_state")]
    [InlineData("room_unavailable")]
    [InlineData("conversation_closed")]
    [InlineData("participant_suspended")]
    public void Every_messaging_code_has_a_spanish_detail(string code)
    {
        var detail = UserResourceProblems.DetailFor(code);

        Assert.False(string.IsNullOrWhiteSpace(detail));
        Assert.DoesNotContain(code, detail!, StringComparison.Ordinal);
    }
}
