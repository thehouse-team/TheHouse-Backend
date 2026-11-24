using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TheHouse.Application.Abstractions.SmartMatch;
using TheHouse.Application.Common;
using TheHouse.Infrastructure.GenAi;
using TheHouse.TestDoubles;

namespace TheHouse.IntegrationTests;

// NV-24 1.2.1 T04 y T05: cliente de TheHouse-GenAI contra un doble HTTP que
// reproduce sus respuestas y errores (sin red): contrato exacto, encabezados,
// timeout, un reintento solo en red/5xx, 429 con Retry-After y circuit breaker.
public sealed class GenAiClientTests
{
    private static readonly GenAiFacts Facts = new(91, "UTTT", "Cuarto con baño privado en residencia estudiantil cerca de UTTT", 2800, 1.3m, ["internet", "baño privado"], 3000, 3, ["internet"]);

    [Fact]
    public async Task Sends_the_frozen_contract_without_room_id_and_with_service_headers()
    {
        var handler = new GenAiHandler(_ => Ok("Te queda a 1.3 km de la UTTT.", generated: true));
        var client = Client(handler);

        var result = await client.ExplainAsync(Facts, "trace-123", default);

        Assert.Equal(GenAiOutcome.Generated, result.Outcome);
        Assert.Equal("explain_match_v3", result.PromptVersion);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://genai.test/api/v1/generative/explain-match", request.Uri);
        Assert.Equal("clave-de-prueba", request.ServiceKey);
        Assert.Equal("trace-123", request.RequestId);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(new[] { "matchScore", "locale", "university", "property", "preferences" }, body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "title", "price", "currency", "distanceKm", "services" }, body.RootElement.GetProperty("property").EnumerateObject().Select(property => property.Name));
        Assert.Equal(new[] { "budgetMax", "currency", "maxDistanceKm", "essentialServices" }, body.RootElement.GetProperty("preferences").EnumerateObject().Select(property => property.Name));
        Assert.Equal("es-MX", body.RootElement.GetProperty("locale").GetString());
        Assert.Equal(JsonValueKind.Number, body.RootElement.GetProperty("property").GetProperty("price").ValueKind);
        Assert.DoesNotContain("roomId", request.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generated_false_is_reported_as_not_generated()
    {
        var client = Client(new GenAiHandler(_ => Ok("Plantilla de GenAI.", generated: false)));

        Assert.Equal(GenAiOutcome.NotGenerated, (await client.ExplainAsync(Facts, null, default)).Outcome);
    }

    [Fact]
    public async Task Server_errors_and_network_errors_are_retried_once()
    {
        var errors = new GenAiHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.Equal(GenAiOutcome.Failed, (await Client(errors).ExplainAsync(Facts, null, default)).Outcome);
        Assert.Equal(2, errors.Requests.Count);

        var calls = 0;
        var flaky = new GenAiHandler(_ => ++calls == 1 ? throw new HttpRequestException("caído") : Ok("Te queda a 1.3 km de la UTTT.", generated: true));
        Assert.Equal(GenAiOutcome.Generated, (await Client(flaky).ExplainAsync(Facts, null, default)).Outcome);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    public async Task Client_errors_are_not_retried(HttpStatusCode status)
    {
        var handler = new GenAiHandler(_ => new HttpResponseMessage(status));

        Assert.Equal(GenAiOutcome.Failed, (await Client(handler).ExplainAsync(Facts, null, default)).Outcome);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Timeout_is_not_retried()
    {
        var handler = new GenAiHandler(_ => throw new TaskCanceledException("timeout"));

        Assert.Equal(GenAiOutcome.Failed, (await Client(handler).ExplainAsync(Facts, null, default)).Outcome);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Too_many_requests_opens_the_circuit_for_retry_after()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var handler = new GenAiHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(20));
            return response;
        });
        var client = Client(handler, time);

        Assert.Equal(GenAiOutcome.Failed, (await client.ExplainAsync(Facts, null, default)).Outcome);
        Assert.Equal(GenAiOutcome.CircuitOpen, (await client.ExplainAsync(Facts, null, default)).Outcome);
        Assert.Single(handler.Requests);

        time.Now += TimeSpan.FromSeconds(21);
        await client.ExplainAsync(Facts, null, default);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Five_failures_open_the_circuit_for_sixty_seconds()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var handler = new GenAiHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        var client = Client(handler, time);

        for (var index = 0; index < GenAiExplanationClient.FailuresToOpen; index++)
        {
            await client.ExplainAsync(Facts, null, default);
        }

        Assert.Equal(GenAiOutcome.CircuitOpen, (await client.ExplainAsync(Facts, null, default)).Outcome);
        time.Now += TimeSpan.FromSeconds(61);
        Assert.Equal(GenAiOutcome.Failed, (await client.ExplainAsync(Facts, null, default)).Outcome);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Theory]
    [InlineData(false, "https://genai.test", "clave", false)]
    [InlineData(true, "http://genai.test", "clave", false)]
    [InlineData(true, "https://genai.test", "", false)]
    [InlineData(true, null, "clave", false)]
    [InlineData(true, "https://genai.test", "clave", true)]
    public void Genai_is_used_only_with_https_url_and_key(bool enabled, string? url, string key, bool usable)
    {
        Assert.Equal(usable, new GenAiOptions { Enabled = enabled, BaseUrl = url, ServiceKey = key }.IsUsable(out _));
    }

    private static GenAiExplanationClient Client(GenAiHandler handler, TimeProvider? time = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://genai.test/") },
            new GenAiOptions { Enabled = true, BaseUrl = "https://genai.test", ServiceKey = "clave-de-prueba", TimeoutSeconds = 8 },
            time ?? TimeProvider.System,
            NullLogger<GenAiExplanationClient>.Instance);

    private static HttpResponseMessage Ok(string explanation, bool generated) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { explanation, generated, promptVersion = "explain_match_v3" }), Encoding.UTF8, "application/json"),
        };

    private sealed class GenAiHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Uri, string? ServiceKey, string? RequestId, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Service-Key", out var keys) ? keys.Single() : null,
                request.Headers.TryGetValues("X-Request-ID", out var ids) ? ids.Single() : null,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }
}
