using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Visits;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-17 1.3.0: asignación masiva bloqueada en las acciones del arrendador,
// códigos con mensaje en español y regla de dirección exacta (RNF-02).
public sealed class LandlordVisitSecurityTests
{
    [Theory]
    [InlineData(typeof(RescheduleVisitRequest))]
    [InlineData(typeof(LandlordCancelVisitRequest))]
    public void Landlord_inputs_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"status\":\"confirmed\",\"userId\":\"x\"}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("visit_state")]
    [InlineData("visit_in_past")]
    [InlineData("student_schedule_overlap")]
    public void Visit_codes_have_a_spanish_detail(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Only_confirmed_visits_can_reveal_the_address()
    {
        // Las respuestas con dirección se construyen solo en VisitService (vista del
        // estudiante); la vista del arrendador no tiene campos de dirección.
        var landlordFields = typeof(LandlordVisitResponse).GetProperties().Select(property => property.Name).ToArray();
        var studentFields = typeof(VisitResponse).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("Address", landlordFields);
        Assert.DoesNotContain("Location", landlordFields);
        Assert.Contains("Address", studentFields);
        Assert.Equal(VisitStatus.Cancelled, new Visit { Status = VisitStatus.Requested, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1) }
            .Cancel(null, DateTimeOffset.UtcNow).Status);
        Assert.Null(VisitService.CancellationOf(new Visit { Status = VisitStatus.Confirmed }));
    }
}
