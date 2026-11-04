using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TheHouse.Infrastructure.Observability;

namespace TheHouse.Configuration;

public static class ObservabilityConfiguration
{
    public static void AddObservability(
        this WebApplicationBuilder builder)
    {
        var options =
            FoundationConfiguration.AddValidated<
                ObservabilityOptions>(
                builder,
                "Observability",
                value => value.IsValid());

        builder.Logging.Configure(
            logging =>
                logging.ActivityTrackingOptions =
                    ActivityTrackingOptions.TraceId |
                    ActivityTrackingOptions.SpanId |
                    ActivityTrackingOptions.ParentId);

        builder.Logging.AddSimpleConsole(
            console =>
                console.IncludeScopes = true);

        if (!options.Enabled)
        {
            return;
        }

        var openTelemetry =
            builder.Services
                .AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource
                        .AddService(
                            serviceName:
                                options.ServiceName,
                            serviceVersion:
                                options.ServiceVersion,
                            serviceInstanceId:
                                Environment.MachineName)
                        .AddAttributes(
                            [
                                new KeyValuePair<
                                    string,
                                    object>(
                                    "deployment.environment.name",
                                    builder.Environment
                                        .EnvironmentName)
                            ]))
                .WithTracing(tracing =>
                {
                    tracing
                        .SetSampler(
                            new ParentBasedSampler(
                                new TraceIdRatioBasedSampler(
                                    options.TraceSampleRatio)))
                        .AddAspNetCoreInstrumentation(
                            instrumentation =>
                            {
                                instrumentation.Filter =
                                    context =>
                                        !context.Request.Path
                                            .StartsWithSegments(
                                                "/health");
                            })
                        .AddSource("MongoDB.Driver")
                        .AddSource(
                            TelemetrySources.RedisName);

                    if (options.OtlpEnabled)
                    {
                        tracing.AddOtlpExporter(
                            exporter =>
                                exporter.Endpoint =
                                    new Uri(
                                        options.OtlpEndpoint!));
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddRuntimeInstrumentation();

                    if (options.OtlpEnabled)
                    {
                        metrics.AddOtlpExporter(
                            exporter =>
                                exporter.Endpoint =
                                    new Uri(
                                        options.OtlpEndpoint!));
                    }
                });
    }
}
