namespace EventPlatform.Infrastructure.Retry;

public sealed class RandomRetryJitterSource : IRetryJitterSource
{
    public double NextUnit() => Random.Shared.NextDouble();
}
