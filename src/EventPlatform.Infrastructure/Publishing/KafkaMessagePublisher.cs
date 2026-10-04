using System.Text;
using Confluent.Kafka;
using EventPlatform.Infrastructure.Configuration;
using EventPlatform.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace EventPlatform.Infrastructure.Publishing;

public sealed class KafkaMessagePublisher : IKafkaMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessagePublisher> _logger;

    public KafkaMessagePublisher(KafkaOptions options, ILogger<KafkaMessagePublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            ClientId = "event-platform",
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000,
            CompressionType = CompressionType.Lz4,
            LingerMs = 5,
            EnableBackgroundPoll = true
        };

        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka producer error: {Reason}", error.Reason))
            .Build();
    }

    public async Task<PublishResult> PublishAsync(
        string topic,
        string key,
        string value,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var activity = EventPlatformTelemetry.ActivitySource.StartActivity("event.publish");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.kafka.message.key", key);

        var kafkaHeaders = new Headers();
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                kafkaHeaders.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
            }
        }

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = key,
                    Value = value,
                    Headers = kafkaHeaders
                },
                cancellationToken);

            activity?.SetTag("messaging.kafka.partition", deliveryResult.Partition.Value);
            activity?.SetTag("messaging.kafka.offset", deliveryResult.Offset.Value);

            return new PublishResult(
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                true);
        }
        catch (ProduceException<string, string> ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Error.Reason);
            _logger.LogError(ex, "Failed to publish Kafka message to topic {Topic}", topic);
            return PublishResult.Failed(topic);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
