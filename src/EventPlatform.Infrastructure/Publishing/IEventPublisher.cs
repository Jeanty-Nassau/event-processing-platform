using EventPlatform.Contracts.Events;

namespace EventPlatform.Infrastructure.Publishing;

public interface IEventPublisher
{
    Task<PublishResult> PublishAsync(EventEnvelopeV1 envelope, CancellationToken cancellationToken);
}
