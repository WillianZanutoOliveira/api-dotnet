using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DistributedCommerce.Observability;

public static class PlatformTelemetry
{
    public const string SourceName = "DistributedCommerce";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> MessagesProcessed =
        Meter.CreateCounter<long>("distributed_commerce.messages.processed");

    public static readonly Counter<long> MessagesFailed =
        Meter.CreateCounter<long>("distributed_commerce.messages.failed");

    public static IServiceCollection AddPlatformObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
                else
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
                else
                {
                    metrics.AddConsoleExporter();
                }
            });

        return services;
    }
}
