using Confluent.Kafka;
using NUnit.Framework;
using Testcontainers.Kafka;

namespace EventPlatform.IntegrationTests;

public sealed class KafkaRoundTripTests
{
    private KafkaContainer _kafka = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _kafka = new KafkaBuilder("confluentinc/cp-kafka:7.9.0").Build();
        await _kafka.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await _kafka.DisposeAsync();
    }

    [Test]
    public async Task MessageKeyAndValue_RoundTripThroughRealKafka()
    {
        var topic = "event-platform-integration-" + Guid.NewGuid().ToString("N");
        var key = "subject-42";
        var value = "integration-message";

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _kafka.GetBootstrapAddress(),
            Acks = Acks.All
        }).Build();

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = value
        });

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _kafka.GetBootstrapAddress(),
            GroupId = "integration-" + Guid.NewGuid().ToString("N"),
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(topic);
        var consumed = consumer.Consume(TimeSpan.FromSeconds(15));

        Assert.That(consumed, Is.Not.Null);
        Assert.That(consumed!.Message.Key, Is.EqualTo(key));
        Assert.That(consumed.Message.Value, Is.EqualTo(value));
    }
}
