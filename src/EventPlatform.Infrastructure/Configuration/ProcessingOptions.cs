namespace EventPlatform.Infrastructure.Configuration;

public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    public int MaxRetryAttempts { get; init; } = 3;

    public int RetryDispatcherBatchSize { get; init; } = 25;

    public int RetryPollIntervalMs { get; init; } = 1000;
}
