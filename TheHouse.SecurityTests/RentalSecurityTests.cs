using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Rentals;
using TheHouse.Application.UseCases;
using TheHouse.Domain.Entities;
using TheHouse.Http;

namespace TheHouse.SecurityTests;

// NV-18: asignación masiva bloqueada, códigos con mensaje en español,
// código de cierre solo en hash, QR opaco y dirección exacta solo en Mi renta.
public sealed class RentalSecurityTests
{
    [Theory]
    [InlineData(typeof(CreateAgreementRequest))]
    [InlineData(typeof(EndAgreementRequest))]
    [InlineData(typeof(ClosingCodeRequest))]
    [InlineData(typeof(AcceptAgreementRequest))]
    public void Rental_inputs_reject_unknown_fields(Type type)
    {
        var attribute = type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, attribute?.UnmappedMemberHandling);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"studentId\":\"x\",\"status\":\"active\"}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData("agreement_open")]
    [InlineData("agreement_state")]
    [InlineData("code_invalid")]
    [InlineData("code_expired")]
    [InlineData("code_used")]
    [InlineData("too_many_attempts")]
    [InlineData("no_active_rental")]
    [InlineData("request_state")]
    [InlineData("room_unavailable")]
    public void Rental_codes_have_a_spanish_detail(string code) =>
        Assert.False(string.IsNullOrWhiteSpace(UserResourceProblems.DetailFor(code)));

    [Fact]
    public void Closing_codes_are_stored_only_as_hashes()
    {
        var stored = typeof(ClosingCode).GetProperties().Select(property => property.Name).ToArray();
        var (code, token) = ClosingCodes.Generate();

        Assert.DoesNotContain("BackupCode", stored);
        Assert.DoesNotContain("QrToken", stored);
        Assert.Contains("CodeHash", stored);
        Assert.Equal(64, ClosingCodes.Hash(code).Length);
        Assert.All(token, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.All(code, character => Assert.DoesNotContain(character, "IO01"));
    }

    [Fact]
    public void Only_my_rental_reveals_the_address_and_nobody_sees_contact_data()
    {
        string[] Fields(Type type) => type.GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("Address", Fields(typeof(LandlordAgreementResponse)));
        Assert.DoesNotContain("Address", Fields(typeof(AgreementPreviewResponse)));
        Assert.DoesNotContain("Location", Fields(typeof(AgreementPreviewResponse)));
        Assert.Contains("Address", Fields(typeof(RentalResponse)));
        Assert.Equal(new[] { "Name", "University" }, Fields(typeof(AgreementStudentSummary)));
        Assert.Equal(new[] { "Name" }, Fields(typeof(AgreementLandlordSummary)));
        foreach (var type in new[] { typeof(LandlordAgreementResponse), typeof(AgreementPreviewResponse), typeof(RentalResponse) })
        {
            Assert.DoesNotContain(Fields(type), name => name is "Phone" or "Email" or "StudentId" or "OwnerId");
        }
    }
}
