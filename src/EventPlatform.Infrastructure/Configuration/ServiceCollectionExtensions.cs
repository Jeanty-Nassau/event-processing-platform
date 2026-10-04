using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EventPlatform.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventSecurityConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EventSecurityOptions>()
            .Bind(configuration.GetSection(EventSecurityOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SigningSecret), "EventSecurity:SigningSecret is required.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<EventSecurityOptions>>().Value);
        return services;
    }

    public static IServiceCollection AddKafkaConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka:BootstrapServers is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.EventsTopic), "Kafka:EventsTopic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterTopic), "Kafka:DeadLetterTopic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConsumerGroup), "Kafka:ConsumerGroup is required.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<KafkaOptions>>().Value);
        return services;
    }

    public static IServiceCollection AddPostgresConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Postgres:ConnectionString is required.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PostgresOptions>>().Value);
        return services;
    }

    public static IServiceCollection AddProcessingConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ProcessingOptions>()
            .Bind(configuration.GetSection(ProcessingOptions.SectionName))
            .Validate(options => options.MaxRetryAttempts > 0, "Processing:MaxRetryAttempts must be greater than zero.")
            .Validate(options => options.RetryDispatcherBatchSize > 0, "Processing:RetryDispatcherBatchSize must be greater than zero.")
            .Validate(options => options.RetryPollIntervalMs > 0, "Processing:RetryPollIntervalMs must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ProcessingOptions>>().Value);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        return services;
    }

    public static IServiceCollection AddEventPlatformPersistence(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var postgresOptions = sp.GetRequiredService<PostgresOptions>();
            return NpgsqlDataSource.Create(postgresOptions.ConnectionString);
        });

        return services;
    }
}
