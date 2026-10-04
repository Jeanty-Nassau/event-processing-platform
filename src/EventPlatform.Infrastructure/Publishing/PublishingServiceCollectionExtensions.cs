using Microsoft.Extensions.DependencyInjection;

namespace EventPlatform.Infrastructure.Publishing;

public static class PublishingServiceCollectionExtensions
{
    public static IServiceCollection AddEventPlatformMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IKafkaMessagePublisher, KafkaMessagePublisher>();
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();
        return services;
    }
}
