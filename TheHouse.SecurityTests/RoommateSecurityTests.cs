using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Roommates;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-22: asignación masiva bloqueada, códigos con mensaje en español y
// tarjetas sin datos que identifiquen o ubiquen al estudiante (RNF-02).
public sealed class RoommateSecurityTests
{
    [Theory]
    [InlineData(typeof(RoommateProfileInput))]
    [InlineData(typeof(RoommateHabitsInput))]
    [InlineData(typeof(ContactRoommateRequest))]
    public void Roommate_inputs_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"userId\":\"x\",\"universityId\":\"y\"}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("roommate_profile_required")]
    [InlineData("university_required")]
    [InlineData("own_profile")]
    [InlineData("participant_suspended")]
    public void Roommate_codes_have_a_spanish_detail(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Cards_never_carry_user_ids_contact_data_or_location()
    {
        var fields = typeof(RoommateCard).GetProperties().Select(property => property.Name).ToArray();

        foreach (var forbidden in new[] { "UserId", "Email", "Phone", "LastName", "Location", "Address", "Latitude" })
        {
            Assert.DoesNotContain(forbidden, fields);
        }

        Assert.DoesNotContain("UserId", typeof(MyRoommateProfileResponse).GetProperties().Select(property => property.Name));
    }
}
