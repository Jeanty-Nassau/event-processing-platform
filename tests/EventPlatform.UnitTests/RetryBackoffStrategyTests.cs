using EventPlatform.Infrastructure.Retry;
using NUnit.Framework;

namespace EventPlatform.UnitTests;

public sealed class RetryBackoffStrategyTests
{
    [TestCase(1, 1000)]
    [TestCase(2, 5000)]
    [TestCase(3, 30000)]
    [TestCase(4, 30000)]
    public void GetDelay_UsesExpectedBaseDelay_WhenJitterIsCentered(int attempt, int expectedMilliseconds)
    {
        var strategy = new RetryBackoffStrategy(new FixedJitterSource(0.5));

        var delay = strategy.GetDelay(attempt);

        Assert.That(delay.TotalMilliseconds, Is.EqualTo(expectedMilliseconds).Within(1));
    }

    [Test]
    public void GetDelay_AppliesBoundedJitter()
    {
        var low = new RetryBackoffStrategy(new FixedJitterSource(0)).GetDelay(1);
        var high = new RetryBackoffStrategy(new FixedJitterSource(1)).GetDelay(1);

        Assert.That(low.TotalMilliseconds, Is.EqualTo(900).Within(1));
        Assert.That(high.TotalMilliseconds, Is.EqualTo(1100).Within(1));
    }

    [Test]
    public void GetDelay_RejectsNonPositiveAttempt()
    {
        var strategy = new RetryBackoffStrategy(new FixedJitterSource(0.5));

        Assert.That(() => strategy.GetDelay(0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private sealed class FixedJitterSource(double value) : IRetryJitterSource
    {
        public double NextUnit() => value;
    }
}
