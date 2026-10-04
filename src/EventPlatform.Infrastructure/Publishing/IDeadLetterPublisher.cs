using EventPlatform.Contracts.Events;

namespace EventPlatform.Infrastructure.Publishing;

public interface IDeadLetterPublisher
{
    Task<PublishResult> PublishAsync(DeadLetterEnvelopeV1 deadLetter, string key, CancellationToken cancellationToken);
}
