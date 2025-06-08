using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Events;

public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<DomainEvent> events, CancellationToken cancellationToken = default);
    Task DispatchEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}