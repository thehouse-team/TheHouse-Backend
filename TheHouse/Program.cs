using TheHouse.Configuration;
using TheHouse.Http;
using TheHouse.Health;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);
builder.AddFoundationConfiguration();
builder.AddRegistration();
builder.AddLogin();

builder.Services.AddFoundationHttp();
builder.Services.AddFoundationOpenApi();
builder.Services.AddFoundationHealth();
builder.Services.AddFoundationRateLimits();
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Unicamente contenido pÃºblico con opt-in explÃ­cito.
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ["application/json", "text/plain"];
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<FoundationExceptionMiddleware>();
app.UseForwardedHeaders();
app.UseStatusCodePages(context => ProblemResponses.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
app.UseMiddleware<RequestBodyLimitMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().WithMetadata(new PublicResponseCompressionAttribute()).DisableRateLimiting();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRequestTimeouts();
app.UseMiddleware<PublicResponseCompressionMiddleware>();
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapFoundationHealth();
app.MapRegistration();
app.MapLogin();

app.Run();

public partial class Program;
