namespace EventPlatform.Infrastructure.Retry;

public sealed class RetryBackoffStrategy : IRetryBackoffStrategy
{
    private static readonly TimeSpan[] BaseDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30)
    ];

    private readonly IRetryJitterSource _jitterSource;

    public RetryBackoffStrategy(IRetryJitterSource jitterSource)
    {
        _jitterSource = jitterSource;
    }

    public TimeSpan GetDelay(int attempt)
    {
        if (attempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        var index = Math.Min(attempt - 1, BaseDelays.Length - 1);
        var baseDelay = BaseDelays[index];
        var factor = 0.9 + (_jitterSource.NextUnit() * 0.2);

        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor);
    }
}
