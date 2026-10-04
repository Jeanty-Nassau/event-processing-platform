namespace EventPlatform.Infrastructure.Publishing;

public interface IKafkaMessagePublisher
{
    Task<PublishResult> PublishAsync(
        string topic,
        string key,
        string value,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken);
}
