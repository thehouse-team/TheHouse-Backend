using System.Text.Json.Serialization;

namespace TheHouse.Application.Contracts.Users;

// NV-10 1.2.0: contratos de eliminación de cuenta y aviso legal (§6.1).
public sealed record DeletionEffectsResponse(
    int Buildings,
    int Rooms,
    int UpcomingVisits,
    int OpenRequests,
    int PendingAgreements,
    int Conversations,
    int Reviews,
    int Favorites,
    bool RoommateProfile);

public sealed record DeletionBlockerResponse(string Code, int Count);

public sealed record DeletionPreviewResponse(
    bool CanDelete,
    IReadOnlyList<DeletionBlockerResponse> Blockers,
    DeletionEffectsResponse Effects);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AccountDeletionRequest(string? Password, string? Confirmation);

public sealed record LegalResponse(
    string TermsVersion,
    string PrivacyVersion,
    string LandlordTermsVersion,
    string? TermsUrl,
    string? PrivacyUrl,
    string? AccountDeletionUrl,
    DateTimeOffset UpdatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegalAcceptanceRequest(string? TermsVersion, string? PrivacyVersion);

public sealed record LegalAcceptanceResponse(string TermsVersion, string PrivacyVersion, DateTimeOffset AcceptedAt);
