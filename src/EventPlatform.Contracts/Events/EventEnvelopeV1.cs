using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventPlatform.Contracts.Events;

public sealed record EventEnvelopeV1
{
    [JsonPropertyName("eventId")]
    public required Guid EventId { get; init; }

    [JsonPropertyName("eventType")]
    public required string EventType { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public required string Source { get; init; } = string.Empty;

    [JsonPropertyName("subjectId")]
    public required string SubjectId { get; init; } = string.Empty;

    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    [JsonPropertyName("occurredAt")]
    public required DateTimeOffset OccurredAt { get; init; }

    [JsonPropertyName("correlationId")]
    public required Guid CorrelationId { get; init; }

    [JsonPropertyName("payload")]
    public required JsonElement Payload { get; init; }
}
