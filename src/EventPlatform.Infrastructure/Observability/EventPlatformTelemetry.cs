using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EventPlatform.Infrastructure.Observability;

public static class EventPlatformTelemetry
{
    public const string ActivitySourceName = "EventPlatform";
    public const string MeterName = "EventPlatform";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> IngestedEvents =
        Meter.CreateCounter<long>("eventplatform.events.ingested");

    public static readonly Counter<long> RejectedEvents =
        Meter.CreateCounter<long>("eventplatform.events.rejected");

    public static readonly Counter<long> ProcessedEvents =
        Meter.CreateCounter<long>("eventplatform.events.processed");

    public static readonly Counter<long> DuplicateEvents =
        Meter.CreateCounter<long>("eventplatform.events.duplicate");

    public static readonly Counter<long> FailedEvents =
        Meter.CreateCounter<long>("eventplatform.events.failed");

    public static readonly Counter<long> ScheduledRetries =
        Meter.CreateCounter<long>("eventplatform.retries.scheduled");

    public static readonly Counter<long> DispatchedRetries =
        Meter.CreateCounter<long>("eventplatform.retries.dispatched");

    public static readonly Counter<long> DeadLetteredEvents =
        Meter.CreateCounter<long>("eventplatform.events.dead_lettered");

    public static readonly Histogram<double> ProcessingDuration =
        Meter.CreateHistogram<double>("eventplatform.processing.duration", unit: "s");
}
