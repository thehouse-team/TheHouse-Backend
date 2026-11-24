using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;

namespace TheHouse.Infrastructure.GenAi;

// NV-24 1.2.1: configuración de TheHouse-GenAI. La clave llega solo como
// secreto de Container Apps (`GenAI__ServiceKey`); nunca en appsettings.
public sealed class GenAiOptions
{
    public bool Enabled { get; init; }

    public string? BaseUrl { get; init; }

    public string? ServiceKey { get; init; }

    public int TimeoutSeconds { get; init; } = 8;

    // Activo solo con URL https absoluta y clave; si no, se usa la plantilla.
    public bool IsUsable(out string? problem)
    {
        problem = !Enabled ? "desactivado"
            : !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ? "BaseUrl no es https"
            : string.IsNullOrWhiteSpace(ServiceKey) ? "falta la clave de servicio"
            : null;
        return problem is null;
    }
}

// Cliente de `POST /api/v1/generative/explain-match` (contrato congelado del
// 2026-11-18). Un intento con timeout; un reintento solo en error de red o
// 5xx; 429 abre el circuito durante `Retry-After`; 5 fallas seguidas lo abren
// 60 s. El estado del circuito es por réplica y solo protege a GenAI.
public sealed class GenAiExplanationClient(
    HttpClient http,
    GenAiOptions options,
    TimeProvider timeProvider,
    ILogger<GenAiExplanationClient> logger) : IGenAiExplanationClient
{
    public const string Path = "api/v1/generative/explain-match";

    public const int FailuresToOpen = 5;

    public static readonly TimeSpan OpenFor = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly object gate = new();

    private int failures;

    private DateTimeOffset openUntil = DateTimeOffset.MinValue;

    public bool Enabled => true;

    public async Task<GenAiExplanationResult> ExplainAsync(GenAiFacts facts, string? requestId, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (timeProvider.GetUtcNow() < openUntil)
            {
                return new GenAiExplanationResult(GenAiOutcome.CircuitOpen);
            }
        }

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var (result, retry) = await SendAsync(facts, requestId, cancellationToken);
            if (!retry || attempt == 2)
            {
                return result;
            }
        }

        return new GenAiExplanationResult(GenAiOutcome.Failed);
    }

    private async Task<(GenAiExplanationResult Result, bool Retry)> SendAsync(GenAiFacts facts, string? requestId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30)));
        using var message = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(Body(facts), options: Json),
        };
        message.Headers.Add("X-Service-Key", options.ServiceKey);
        if (!string.IsNullOrWhiteSpace(requestId) && requestId.Length <= 128)
        {
            message.Headers.Add("X-Request-ID", requestId);
        }

        try
        {
            using var response = await http.SendAsync(message, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
                lock (gate)
                {
                    openUntil = timeProvider.GetUtcNow() + wait;
                }

                logger.LogWarning("NV-24: TheHouse-GenAI respondió 429; se usa la plantilla durante {Seconds} s.", (int)wait.TotalSeconds);
                return (new GenAiExplanationResult(GenAiOutcome.Failed), false);
            }

            if ((int)response.StatusCode >= 500)
            {
                Fail();
                logger.LogWarning("NV-24: TheHouse-GenAI respondió {Status}.", (int)response.StatusCode);
                return (new GenAiExplanationResult(GenAiOutcome.Failed), true);
            }

            if (!response.IsSuccessStatusCode)
            {
                // 401 o 422: error de configuración o de código; sin el cuerpo.
                Fail();
                logger.LogError("NV-24: TheHouse-GenAI rechazó la solicitud con {Status} (revisar la clave o el contrato).", (int)response.StatusCode);
                return (new GenAiExplanationResult(GenAiOutcome.Failed), false);
            }

            var body = await response.Content.ReadFromJsonAsync<ExplainMatchResponse>(Json, timeout.Token);
            Succeed();
            return body is { Explanation: not null }
                ? (new GenAiExplanationResult(body.Generated ? GenAiOutcome.Generated : GenAiOutcome.NotGenerated, body.Explanation, body.PromptVersion), false)
                : (new GenAiExplanationResult(GenAiOutcome.Failed), false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout (incluye el arranque en frío de GenAI): sin reintento.
            Fail();
            logger.LogWarning("NV-24: TheHouse-GenAI no respondió en {Seconds} s.", options.TimeoutSeconds);
            return (new GenAiExplanationResult(GenAiOutcome.Failed), false);
        }
        catch (HttpRequestException exception)
        {
            Fail();
            logger.LogWarning("NV-24: TheHouse-GenAI no disponible ({Reason}).", exception.HttpRequestError);
            return (new GenAiExplanationResult(GenAiOutcome.Failed), true);
        }
        catch (JsonException)
        {
            Fail();
            logger.LogWarning("NV-24: TheHouse-GenAI devolvió un JSON inesperado.");
            return (new GenAiExplanationResult(GenAiOutcome.Failed), false);
        }
    }

    // Cuerpo exacto del contrato: sin id del cuarto ni campos extra.
    internal static ExplainMatchRequest Body(GenAiFacts facts) =>
        new(
            facts.MatchScore,
            "es-MX",
            facts.University,
            new PropertyFacts(facts.Title, facts.Price, "MXN", facts.DistanceKm, facts.Services),
            new PreferenceFacts(facts.BudgetMax, "MXN", facts.MaxDistanceKm, facts.EssentialServices));

    private void Fail()
    {
        lock (gate)
        {
            if (++failures >= FailuresToOpen)
            {
                openUntil = timeProvider.GetUtcNow() + OpenFor;
                failures = 0;
            }
        }
    }

    private void Succeed()
    {
        lock (gate)
        {
            failures = 0;
        }
    }

    internal sealed record ExplainMatchRequest(int MatchScore, string Locale, string University, PropertyFacts Property, PreferenceFacts Preferences);

    internal sealed record PropertyFacts(string Title, decimal Price, string Currency, decimal DistanceKm, IReadOnlyList<string> Services);

    internal sealed record PreferenceFacts(decimal BudgetMax, string Currency, decimal MaxDistanceKm, IReadOnlyList<string> EssentialServices);

    private sealed record ExplainMatchResponse(string? Explanation, bool Generated, string? PromptVersion);
}
