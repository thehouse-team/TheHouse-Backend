using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TheHouse.Http;
using TheHouse.Application.Pagination;

namespace TheHouse.IntegrationTests;

[ApiController]
[Route("api/v1/foundation-test")]
public sealed class FoundationTestController : ControllerBase
{
    [HttpGet("exception/{kind}")]
    public IActionResult ExceptionProbe(string kind) => throw kind switch
    {
        "validation" => new ValidationException("internal-marker-never-public"),
        "bad-request" => new BadHttpRequestException("internal-marker-never-public", 400),
        "timeout" => new TimeoutException("internal-marker-never-public"),
        "cancel" => new OperationCanceledException("internal-marker-never-public"),
        _ => new InvalidOperationException("internal-marker-never-public")
    };

    [HttpPost("validate")]
    public IActionResult ValidateProbe(ProbeInput input) => Ok(new { input.Count });

    [HttpGet("timeout")]
    public async Task<IActionResult> TimeoutProbe()
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpGet("context")]
    public IActionResult ContextProbe() => Ok(new
    {
        traceId = HttpContext.TraceIdentifier,
        scheme = Request.Scheme,
        host = Request.Host.Value,
        ip = HttpContext.Connection.RemoteIpAddress?.ToString()
    });

    [HttpGet("public/{size:int}")]
    [PublicResponseCompression]
    public IActionResult PublicProbe(int size, string? variant = null)
    {
        if (variant == "cookie") Response.Headers.SetCookie = "example=fixture; Secure; HttpOnly";
        if (variant == "private") Response.Headers.CacheControl = "private";
        if (variant == "no-store") Response.Headers.CacheControl = "no-store";
        if (variant == "no-transform") Response.Headers.CacheControl = "no-transform";
        return Content(new string('x', size), variant == "mime" ? "application/octet-stream" : "text/plain");
    }

    [HttpGet("sensitive")]
    public IActionResult SensitiveProbe() => Content(new string('x', 4096), "text/plain");

    [HttpGet("limited")]
    [EnableRateLimiting(FoundationRateLimits.PerEndpoint)]
    public IActionResult LimitedProbe() => Ok();

    [HttpGet("pagination")]
    public IActionResult PaginationProbe([FromQuery] PageRequest request) =>
        Ok(PageResult<int>.Create([], request, 0));
}

public sealed class ProbeInput
{
    [Required, StringLength(40)]
    public string? Name { get; set; }

    [Range(1, 50)]
    public int Count { get; set; }
}
