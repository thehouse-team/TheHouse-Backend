using System.Text.Json.Serialization;
using TheHouse.Application.Contracts.Profile;
using TheHouse.Application.Contracts.Search;

namespace TheHouse.Application.Contracts.SmartMatch;

// NV-23 1.2.0 §6.1: contratos estrictos (campos desconocidos → 400, OWASP API3).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SmartMatchScoresRequest
{
    public IReadOnlyList<string>? RoomIds { get; init; }

    // Solo sin token (invitado); con token se usan el perfil y las preferencias guardadas.
    public string? UniversityId { get; init; }

    public PreferencesRequest? Preferences { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SmartMatchQueryRequest
{
    public string? UniversityId { get; init; }

    public PreferencesRequest? Preferences { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SmartMatchFeedbackRequest
{
    public string? RoomId { get; init; }

    public bool? Useful { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SmartMatchDismissalRequest
{
    public string? RoomId { get; init; }
}

// Desglose público: sin los valores de las features (minimización, §10).
public sealed record SmartMatchFactorResponse(string Factor, string Label, double Points, double MaxPoints);

public sealed record SmartMatchScoreItem(
    string RoomId,
    bool Eligible,
    IReadOnlyList<string> ExcludedBy,
    int? MatchScore,
    IReadOnlyList<SmartMatchFactorResponse> Breakdown,
    string ModelVersion,
    string FeatureSchemaVersion);

public sealed record SmartMatchScoresResponse(IReadOnlyList<SmartMatchScoreItem> Items);

public sealed record SmartMatchRecommendation(
    RoomSearchItem Room,
    int MatchScore,
    IReadOnlyList<SmartMatchFactorResponse> Breakdown,
    bool Dismissed,
    string ModelVersion,
    string FeatureSchemaVersion);

public sealed record SmartMatchRecommendationsResponse(
    IReadOnlyList<SmartMatchRecommendation> Items,
    int Page,
    int PageSize,
    long TotalItems,
    int TotalPages);

// RF-66: métricas agregadas, sin ids de usuario ni de cuarto.
public sealed record SmartMatchMetricsTotals(
    long ScoreRequests,
    long RecommendationRequests,
    long GuestRequests,
    long RoomsScored,
    long EligibleRooms,
    double? AverageMatchScore,
    IReadOnlyDictionary<string, long> ScoreBuckets,
    long FeedbackUseful,
    long FeedbackNotUseful,
    long ActiveDismissals);

public sealed record SmartMatchModelMetrics(
    string ModelVersion,
    long RoomsScored,
    double? AverageMatchScore,
    long FeedbackUseful,
    long FeedbackNotUseful);

public sealed record SmartMatchMetricsResponse(
    string From,
    string To,
    SmartMatchMetricsTotals Totals,
    IReadOnlyList<SmartMatchModelMetrics> ByModelVersion);
