using System.Text.Json;
using EventPlatform.Contracts.Events;

namespace EventPlatform.Infrastructure.Publishing;

public sealed class KafkaEventPublisher : IEventPublisher
{
    private readonly IKafkaMessagePublisher _publisher;
    private readonly Configuration.KafkaOptions _options;

    public KafkaEventPublisher(IKafkaMessagePublisher publisher, Configuration.KafkaOptions options)
    {
        _publisher = publisher;
        _options = options;
    }

    public Task<PublishResult> PublishAsync(EventEnvelopeV1 envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var headers = new Dictionary<string, string>
        {
            ["event-id"] = envelope.EventId.ToString(),
            ["event-type"] = envelope.EventType,
            ["schema-version"] = envelope.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["correlation-id"] = envelope.CorrelationId.ToString(),
            ["source"] = envelope.Source
        };

        return _publisher.PublishAsync(
            _options.EventsTopic,
            envelope.SubjectId,
            JsonSerializer.Serialize(envelope),
            headers,
            cancellationToken);
    }
}
