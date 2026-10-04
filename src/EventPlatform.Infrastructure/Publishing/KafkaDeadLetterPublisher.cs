using System.Text.Json;
using EventPlatform.Contracts.Events;
using EventPlatform.Infrastructure.Configuration;

namespace EventPlatform.Infrastructure.Publishing;

public sealed class KafkaDeadLetterPublisher : IDeadLetterPublisher
{
    private readonly IKafkaMessagePublisher _publisher;
    private readonly KafkaOptions _options;

    public KafkaDeadLetterPublisher(IKafkaMessagePublisher publisher, KafkaOptions options)
    {
        _publisher = publisher;
        _options = options;
    }

    public Task<PublishResult> PublishAsync(DeadLetterEnvelopeV1 deadLetter, string key, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            ["failure-category"] = deadLetter.FailureCategory,
            ["reason-code"] = deadLetter.ReasonCode,
            ["attempt-count"] = deadLetter.AttemptCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (deadLetter.EventId.HasValue)
        {
            headers["event-id"] = deadLetter.EventId.Value.ToString();
        }

        return _publisher.PublishAsync(
            _options.DeadLetterTopic,
            key,
            JsonSerializer.Serialize(deadLetter),
            headers,
            cancellationToken);
    }
}
