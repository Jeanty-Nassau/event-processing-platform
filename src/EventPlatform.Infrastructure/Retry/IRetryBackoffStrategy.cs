namespace EventPlatform.Infrastructure.Retry;

public interface IRetryBackoffStrategy
{
    TimeSpan GetDelay(int attempt);
}
