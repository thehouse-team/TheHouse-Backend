using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Rentals;

// NV-18 §6.1: convenios, código de cierre y Mi renta. Las entradas rechazan
// campos desconocidos (asignación masiva): ni `studentId`, ni `roomId`, ni
// `status` llegan del cliente.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateAgreementRequest
{
    public string? RequestId { get; init; }

    public int? MonthlyRent { get; init; }

    public int? Deposit { get; init; }

    public string? StartDate { get; init; }

    public int? DurationMonths { get; init; }

    public int? PaymentDay { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EndAgreementRequest
{
    public string? Reason { get; init; }

    public string? Note { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ClosingCodeRequest
{
    public string? Code { get; init; }

    public string? QrToken { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AcceptAgreementRequest
{
    public string? Code { get; init; }

    public string? QrToken { get; init; }

    public bool? AcceptedTerms { get; init; }
}

public sealed record AgreementRoomSummary(string Id, string BuildingId, string Title, string City);

public sealed record AgreementTermsResponse(
    int MonthlyRent,
    int Deposit,
    DateOnly StartDate,
    int DurationMonths,
    DateOnly EndDate,
    int PaymentDay,
    string Currency);

// Solo la ve el arrendador: nombre del perfil (NV-11) y universidad.
public sealed record AgreementStudentSummary(string? Name, string? University);

// Solo en la respuesta de crear y de `/code`.
public sealed record ClosingCodeResponse(string QrToken, string BackupCode, DateTimeOffset ExpiresAt);

public sealed record LandlordAgreementResponse(
    string Id,
    AgreementRoomSummary Room,
    AgreementStudentSummary Student,
    AgreementTermsResponse Terms,
    string Status,
    ClosingCodeResponse? ClosingCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset? CancelledAt);

public sealed record LandlordAgreementListResponse(
    IReadOnlyList<LandlordAgreementResponse> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

public sealed record AgreementBuildingSummary(string Name, string? Rules);

// Nombre sin apellidos, como en NV-16 y NV-17.
public sealed record AgreementLandlordSummary(string Name);

public sealed record AgreementPreviewResponse(
    string Id,
    AgreementRoomSummary Room,
    AgreementBuildingSummary Building,
    AgreementLandlordSummary Landlord,
    AgreementTermsResponse Terms,
    DateTimeOffset CodeExpiresAt);

public sealed record RentalNextPayment(DateOnly DueDate, int Amount);

public sealed record RentalAddress(
    string Street,
    string Number,
    string Neighborhood,
    string City,
    string State,
    string PostalCode);

public sealed record RentalLocation(double Lat, double Lng);

// Mi renta: dirección exacta y ubicación solo con el convenio activo (RNF-02).
public sealed record RentalResponse(
    string AgreementId,
    AgreementRoomSummary Room,
    AgreementBuildingSummary Building,
    AgreementTermsResponse Terms,
    RentalNextPayment? NextPayment,
    RentalAddress Address,
    RentalLocation Location,
    AgreementLandlordSummary Landlord,
    string ConversationId,
    DateTimeOffset? ActivatedAt);
