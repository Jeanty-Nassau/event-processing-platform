using Microsoft.Extensions.DependencyInjection;

namespace EventPlatform.Infrastructure.Retry;

public static class RetryServiceCollectionExtensions
{
    public static IServiceCollection AddEventPlatformRetryPolicy(this IServiceCollection services)
    {
        services.AddSingleton<IRetryJitterSource, RandomRetryJitterSource>();
        services.AddSingleton<IRetryBackoffStrategy, RetryBackoffStrategy>();
        return services;
    }
}
