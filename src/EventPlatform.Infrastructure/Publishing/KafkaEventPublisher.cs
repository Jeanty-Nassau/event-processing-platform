using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using EventPlatform.Contracts.Events;
using EventPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventPlatform.Infrastructure.Publishing;

public sealed class KafkaEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly KafkaOptions _options;

    public KafkaEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaEventPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            ClientId = "event-platform-api",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000,
            CompressionType = CompressionType.Lz4,
            LingerMs = 5,
            EnableBackgroundPoll = true
        };

        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetErrorHandler((_, e) => _logger.LogError(e, "Kafka producer error"))
            .Build();
    }

    public async Task<PublishResult> PublishAsync(EventEnvelopeV1 envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var serialized = JsonSerializer.Serialize(envelope);
        var headers = new Headers
        {
            { "event-id", Encoding.UTF8.GetBytes(envelope.EventId.ToString()) },
            { "event-type", Encoding.UTF8.GetBytes(envelope.EventType) },
            { "schema-version", Encoding.UTF8.GetBytes(envelope.SchemaVersion.ToString()) },
            { "correlation-id", Encoding.UTF8.GetBytes(envelope.CorrelationId.ToString()) },
            { "source", Encoding.UTF8.GetBytes(envelope.Source) }
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                _options.EventsTopic,
                new Message<string, string>
                {
                    Key = envelope.SubjectId,
                    Value = serialized,
                    Headers = headers
                },
                cancellationToken);

            _logger.LogInformation(
                "Published event {EventId} to topic {Topic} partition {Partition} offset {Offset}",
                envelope.EventId,
                deliveryResult.Topic,
                deliveryResult.Partition,
                deliveryResult.Offset);

            return new PublishResult(deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, true);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventId} to Kafka topic {Topic}", envelope.EventId, _options.EventsTopic);
            return PublishResult.Failed(_options.EventsTopic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        await ValueTask.CompletedTask;
    }
}
