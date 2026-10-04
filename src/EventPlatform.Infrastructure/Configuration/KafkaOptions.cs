namespace EventPlatform.Infrastructure.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = string.Empty;

    public string EventsTopic { get; init; } = "event-platform.events.v1";

    public string DeadLetterTopic { get; init; } = "event-platform.events.dlq.v1";

    public string ConsumerGroup { get; init; } = "event-platform-processors-v1";
}
