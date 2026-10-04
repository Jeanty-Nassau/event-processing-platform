using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventPlatform.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventPlatformConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EventSecurityOptions>()
            .Bind(configuration.GetSection(EventSecurityOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SigningSecret), "EventSecurity:SigningSecret is required.")
            .ValidateOnStart();

        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka:BootstrapServers is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.EventsTopic), "Kafka:EventsTopic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterTopic), "Kafka:DeadLetterTopic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConsumerGroup), "Kafka:ConsumerGroup is required.")
            .ValidateOnStart();

        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Postgres:ConnectionString is required.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EventSecurityOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<KafkaOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PostgresOptions>>().Value);

        return services;
    }
}
