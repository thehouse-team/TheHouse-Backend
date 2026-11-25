using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TheHouse.Infrastructure.Moderation;
using TheHouse.Infrastructure.Observability;

namespace TheHouse.Configuration;

public static class ObservabilityConfiguration
{
    // NV-03 1.3.0-u2 (§7.2): métricas que se exportan; el resto se descarta
    // para cuidar el tope diario de ingestión.
    public static readonly IReadOnlySet<string> ExportedMetrics =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "http.server.request.duration",
            "http.server.active_requests",
            "dotnet.process.cpu.time",
            "dotnet.process.memory.working_set",
            "dotnet.gc.collections",
            "dotnet.thread_pool.queue.length",
        };

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

        var azureMonitor =
            options.AzureMonitorEnabled
                ? AzureMonitorExporter(builder, options)
                : null;

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

                    if (azureMonitor is not null)
                    {
                        // HttpClient de .NET 10 emite sus propias trazas
                        // (GenAI, Content Safety y FCM) con la query redactada.
                        tracing
                            .AddSource("System.Net.Http")
                            .AddAzureMonitorTraceExporter(azureMonitor);
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddView(instrument =>
                            ExportedMetrics.Contains(instrument.Name)
                                ? null
                                : MetricStreamConfiguration.Drop);

                    if (options.OtlpEnabled)
                    {
                        metrics.AddOtlpExporter(
                            exporter =>
                                exporter.Endpoint =
                                    new Uri(
                                        options.OtlpEndpoint!));
                    }

                    if (azureMonitor is not null)
                    {
                        metrics.AddAzureMonitorMetricExporter(azureMonitor);
                    }
                });

        if (azureMonitor is not null)
        {
            // Solo Warning y superiores van a Application Insights, con el
            // operation_Id de su traza; la consola sigue con todos los niveles.
            openTelemetry.WithLogging(
                logging => logging.AddAzureMonitorLogExporter(azureMonitor),
                logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = false;
                });

            builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(
                null,
                LogLevel.Warning);
        }
    }

    private static Action<AzureMonitorExporterOptions> AzureMonitorExporter(
        WebApplicationBuilder builder,
        ObservabilityOptions options)
    {
        var identity = new ContainerAppsIdentityTokenSource(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"),
            Environment.GetEnvironmentVariable("IDENTITY_HEADER"),
            string.IsNullOrWhiteSpace(options.IdentityClientId)
                ? builder.Configuration["ContentSafety:IdentityClientId"]
                : options.IdentityClientId,
            TimeProvider.System,
            ContainerAppsIdentityTokenSource.MonitorResource);
        var credential = new ManagedIdentityTokenCredential(identity, TimeProvider.System);
        var samplingRatio = (float)options.TraceSampleRatio;

        return exporter =>
        {
            exporter.ConnectionString = options.AzureMonitorConnectionString;
            exporter.Credential = credential;
            // El contenedor no tiene disco persistente: sin reintentos en disco.
            exporter.DisableOfflineStorage = true;
            exporter.SamplingRatio = samplingRatio;
            exporter.TracesPerSecond = null;
            exporter.EnablePerformanceCounters = false;
            exporter.EnableTraceBasedLogsSampler = false;
        };
    }
}
