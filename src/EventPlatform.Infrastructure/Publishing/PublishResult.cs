namespace EventPlatform.Infrastructure.Publishing;

public sealed record PublishResult(string Topic, int Partition, long Offset, bool IsSuccess)
{
    public static PublishResult Failed(string topic) => new(topic, -1, -1, false);
}
