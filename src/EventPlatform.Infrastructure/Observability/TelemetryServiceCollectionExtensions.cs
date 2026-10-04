using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EventPlatform.Infrastructure.Observability;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddEventPlatformTelemetry(
        this IServiceCollection services,
        string serviceName,
        bool instrumentAspNetCore = false)
    {
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(EventPlatformTelemetry.ActivitySourceName);

                if (instrumentAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(EventPlatformTelemetry.MeterName);

                if (instrumentAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics.AddOtlpExporter();
            });

        return services;
    }
}
