using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;
using TheHouse.Application.Contracts.SmartMatch;

namespace TheHouse.Application.UseCases;

// NV-24 1.2.1: explica el puntaje de NV-23 de un cuarto. Ventajas y
// desventajas siempre deterministas; el resumen lo redacta TheHouse-GenAI si
// está activo y su texto pasa la validación, y si no, la plantilla (RNF-16).
public sealed class SmartMatchExplanationService(
    SmartMatchService smartMatch,
    IGenAiExplanationClient genAi,
    ISmartMatchExplanationCache cache,
    TimeProvider timeProvider,
    ISmartMatchExplanationEvents? events = null)
{
    // Códigos del catálogo de NV-08: un resumen que nombre otra universidad se rechaza.
    private static readonly IReadOnlyCollection<string> UniversityCodes =
        [.. UniversitySeedData.Create().Select(university => university.Code)];

    public Task<SmartMatchExplanationResponse> ExplainAsync(string userId, string? roomId, string? requestId, CancellationToken cancellationToken) =>
        ExplainAsync(userId, roomId, null, requestId, cancellationToken);

    public Task<SmartMatchExplanationResponse> QueryAsync(SmartMatchExplanationQueryRequest request, string? requestId, CancellationToken cancellationToken) =>
        ExplainAsync(null, request.RoomId, request, requestId, cancellationToken);

    private async Task<SmartMatchExplanationResponse> ExplainAsync(
        string? userId,
        string? roomId,
        SmartMatchExplanationQueryRequest? guest,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var context = await smartMatch.ExplanationContextAsync(userId, roomId, guest?.UniversityId, guest?.Preferences, cancellationToken);
        var score = context.Score;
        var now = timeProvider.GetUtcNow();
        var key = SmartMatchExplanationRules.CacheKey(context.Room.Id, SmartMatchExplanationRules.PreferencesHash(context.Student), score.ModelVersion);

        if (await cache.GetAsync(key, now, cancellationToken) is { } cached)
        {
            return Response(context, cached);
        }

        var (pros, cons) = SmartMatchExplanationRules.ProsAndCons(score);
        var template = new CachedExplanation(SmartMatchExplanationRules.TemplateSummary(score), pros, cons, SmartMatchExplanationRules.GeneratedByTemplate);
        if (!genAi.Enabled)
        {
            await cache.SetAsync(key, template, now, cancellationToken);
            return Response(context, template);
        }

        var facts = Facts(context);
        var result = await genAi.ExplainAsync(facts, requestId, cancellationToken);
        if (result.Outcome == GenAiOutcome.Generated)
        {
            var reason = SmartMatchExplanationRules.Reject(result.Explanation, facts, UniversityCodes);
            if (reason is null)
            {
                var generated = template with { Summary = result.Explanation!.Trim(), GeneratedBy = SmartMatchExplanationRules.GeneratedByGenAi };
                await cache.SetAsync(key, generated, now, cancellationToken);
                return Response(context, generated);
            }

            // Se cuenta el rechazo sin registrar el texto.
            events?.Rejected(reason);
        }

        // Sin IA por falla o rechazo: plantilla sin guardar, para reintentar después.
        return Response(context, template);
    }

    private static GenAiFacts Facts(SmartMatchExplanationContext context)
    {
        var room = context.Candidate;
        var student = context.Student;
        var code = context.University.Code;
        return new GenAiFacts(
            context.Score.MatchScore,
            code,
            SmartMatchExplanationRules.NeutralTitle(room.PlaceType, room.PropertyType, code),
            room.PriceMxn,
            Math.Round((decimal)room.DistanceKm, 1, MidpointRounding.AwayFromZero),
            SmartMatchExplanationRules.GenAiServices(room.Services, context.Room.Bathroom == "private"),
            student.BudgetMax,
            Math.Round((decimal)student.MaxDistanceKm, 2, MidpointRounding.AwayFromZero),
            SmartMatchExplanationRules.GenAiServices(student.EssentialServices));
    }

    private static SmartMatchExplanationResponse Response(SmartMatchExplanationContext context, CachedExplanation explanation) =>
        new(
            context.Room.Id,
            context.Score.MatchScore,
            explanation.Summary,
            explanation.Pros,
            explanation.Cons,
            explanation.GeneratedBy,
            context.Score.ModelVersion,
            SmartMatchExplanationRules.ExplanationVersion);
}

// Conteo de textos de GenAI rechazados por la validación del backend (sin el texto).
public interface ISmartMatchExplanationEvents
{
    void Rejected(string reason);
}
