using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Reviews;
using TheHouse.Application.UseCases;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-19: asignación masiva bloqueada, códigos con mensaje en español, autor
// sin datos de contacto y moderación con el objetivo `review`.
public sealed class ReviewSecurityTests
{
    [Theory]
    [InlineData(typeof(ReviewInput))]
    [InlineData(typeof(LandlordScoresInput))]
    [InlineData(typeof(PropertyScoresInput))]
    public void Review_inputs_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"authorId\":\"x\",\"status\":\"published\"}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("own_room")]
    [InlineData("no_stay")]
    [InlineData("review_exists")]
    [InlineData("review_locked")]
    [InlineData("review_state")]
    public void Review_codes_have_a_spanish_detail(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Authors_appear_only_by_first_name_and_reviews_can_be_reported()
    {
        Assert.Equal(new[] { "Name" }, typeof(ReviewAuthor).GetProperties().Select(property => property.Name));
        foreach (var type in new[] { typeof(ReviewResponse), typeof(MyReviewResponse), typeof(RoomReviewListResponse) })
        {
            Assert.DoesNotContain(type.GetProperties(), property => property.Name is "AuthorId" or "OwnerId" or "Email" or "Phone");
        }

        Assert.Contains("review", ModerationRules.ReportTargets);
        Assert.Contains("hide_review", ModerationRules.ReportActions);
    }
}
