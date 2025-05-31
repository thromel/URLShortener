using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.Infrastructure.Services;

public class EventStore : IEventStore
{
    private readonly UrlShortenerDbContext _context;
    private readonly ILogger<EventStore> _logger;

    public EventStore(UrlShortenerDbContext context, ILogger<EventStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SaveEventsAsync(Guid aggregateId, IEnumerable<DomainEvent> events, int expectedVersion)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Check current version for concurrency control
            var currentVersion = await _context.Events
                .Where(e => e.AggregateId == aggregateId)
                .MaxAsync(e => (int?)e.Version) ?? 0;

            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException(
                    $"Expected version {expectedVersion} but current version is {currentVersion} for aggregate {aggregateId}");
            }

            var eventEntities = new List<EventEntity>();

            foreach (var domainEvent in events)
            {
                var eventEntity = new EventEntity
                {
                    EventId = domainEvent.EventId,
                    AggregateId = domainEvent.AggregateId,
                    EventType = domainEvent.GetType().Name,
                    EventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = domainEvent.OccurredAt,
                    Version = domainEvent.Version
                };

                eventEntities.Add(eventEntity);
            }

            _context.Events.AddRange(eventEntities);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogDebug("Saved {EventCount} events for aggregate {AggregateId}",
                events.Count(), aggregateId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to save events for aggregate {AggregateId}", aggregateId);
            throw;
        }
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.Version)
            .AsNoTracking()
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion)
    {
        var eventEntities = await _context.Events
            .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .AsNoTracking()
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsByTypeAsync(string eventType, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Events
            .Where(e => e.EventType == eventType)
            .AsNoTracking();

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.OccurredAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.OccurredAt <= toDate.Value);
        }

        var eventEntities = await query
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();

        return eventEntities.Select(DeserializeEvent);
    }

    public async Task<bool> ExistsAsync(Guid aggregateId)
    {
        return await _context.Events
            .AnyAsync(e => e.AggregateId == aggregateId);
    }

    private static DomainEvent DeserializeEvent(EventEntity eventEntity)
    {
        var eventType = eventEntity.EventType switch
        {
            nameof(UrlCreatedEvent) => typeof(UrlCreatedEvent),
            nameof(UrlAccessedEvent) => typeof(UrlAccessedEvent),
            nameof(UrlExpiredEvent) => typeof(UrlExpiredEvent),
            nameof(UrlDisabledEvent) => typeof(UrlDisabledEvent),
            _ => throw new ArgumentException($"Unknown event type: {eventEntity.EventType}")
        };

        var domainEvent = JsonSerializer.Deserialize(eventEntity.EventData, eventType) as DomainEvent;

        if (domainEvent == null)
        {
            throw new InvalidOperationException($"Failed to deserialize event {eventEntity.EventId}");
        }

        // Set the version from the stored event
        return domainEvent with { Version = eventEntity.Version };
    }
}