using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public record StoredEvent(
    Guid EventId,
    string EventType,
    string EventData,
    DateTime OccurredAt,
    int Version
);

public record EventStream(
    Guid Id,
    List<StoredEvent> Events,
    int Version = 0
);

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

public interface IEventStore
{
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<DomainEvent> events, int expectedVersion);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId);
    Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion);
    Task<IEnumerable<DomainEvent>> GetEventsByTypeAsync(string eventType, DateTime? fromDate = null, DateTime? toDate = null);
    Task<bool> ExistsAsync(Guid aggregateId);
}