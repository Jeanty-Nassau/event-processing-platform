namespace EventPlatform.Contracts.Events;

public sealed record DeadLetterEnvelopeV1
{
    public Guid? EventId { get; init; }

    public string? EventType { get; init; }

    public required string FailureCategory { get; init; }

    public required string ReasonCode { get; init; }

    public required int AttemptCount { get; init; }

    public required string OriginalTopic { get; init; }

    public required int OriginalPartition { get; init; }

    public required long OriginalOffset { get; init; }

    public required DateTimeOffset FailedAt { get; init; }

    public required string RawPayload { get; init; }
}
