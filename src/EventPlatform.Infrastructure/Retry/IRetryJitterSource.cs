namespace EventPlatform.Infrastructure.Retry;

public interface IRetryJitterSource
{
    double NextUnit();
}
