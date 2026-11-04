using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TheHouse.Http;

namespace TheHouse.IntegrationTests;

public sealed class CorrelationTests
{
    [Theory]
    [InlineData("request-01_A.b")]
    [InlineData("a")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234")]
    public async Task Valid_correlation_is_echoed_and_used_by_errors(string id)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.Header, id);
        using var response = await client.GetAsync("/api/v1/foundation-test/exception/unknown");
        await ErrorContractTests.AssertProblem(response, 500);
        Assert.Equal(id, response.Headers.GetValues(CorrelationIdMiddleware.Header).Single());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, body.RootElement.GetProperty("traceId").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("a,b")]
    [InlineData("á")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public async Task Untrusted_correlation_is_replaced(string id)
    {
        using var factory = new ProbeFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdMiddleware.Header, id);
        using var response = await client.GetAsync("/api/v1/foundation-test/context");
        var generated = response.Headers.GetValues(CorrelationIdMiddleware.Header).Single();
        Assert.NotEqual(id, generated);
        Assert.True(Guid.TryParseExact(generated, "N", out _));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(generated, body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Duplicate_correlation_is_replaced()
    {
        using var factory = new FoundationFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(CorrelationIdMiddleware.Header, new[] { "first", "second" });
        using var response = await client.GetAsync("/health/live");
        Assert.True(Guid.TryParseExact(response.Headers.GetValues(CorrelationIdMiddleware.Header).Single(), "N", out _));
    }

    [Fact]
    public async Task Correlation_is_tagged_on_the_current_activity()
    {
        using var activity = new Activity("correlation-test").Start();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.Header] = "activity-test";
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(context);
        Assert.Equal("activity-test", activity.GetTagItem("thehouse.correlation_id"));
        Assert.Equal("activity-test", context.TraceIdentifier);
    }
}
