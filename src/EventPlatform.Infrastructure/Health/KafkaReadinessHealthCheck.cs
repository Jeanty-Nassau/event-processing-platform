using Confluent.Kafka;
using EventPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EventPlatform.Infrastructure.Health;

public sealed class KafkaReadinessHealthCheck : IHealthCheck
{
    private readonly KafkaOptions _options;

    public KafkaReadinessHealthCheck(KafkaOptions options)
    {
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.BootstrapServers
            }).Build();

            var metadata = admin.GetMetadata(_options.EventsTopic, TimeSpan.FromSeconds(2));
            var topic = metadata.Topics.FirstOrDefault();

            if (topic is null || topic.Error.Code != ErrorCode.NoError)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Kafka event topic is not available."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Kafka event topic is available."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka is unavailable.", ex));
        }
    }
}
