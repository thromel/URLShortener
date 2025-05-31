using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Exceptions;
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
            // Check for concurrency conflicts
            var currentVersion = await GetCurrentVersionAsync(aggregateId);
            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException(
                    $"Concurrency conflict for aggregate {aggregateId}. Expected version {expectedVersion}, but current version is {currentVersion}");
            }

            foreach (var @event in events)
            {
                var eventEntity = new EventEntity
                {
                    EventId = @event.EventId,
                    AggregateId = aggregateId,
                    EventType = @event.GetType().Name,
                    EventData = JsonSerializer.Serialize(@event, @event.GetType()),
                    OccurredAt = @event.OccurredAt,
                    Version = @event.Version
                };

                _context.Events.Add(eventEntity);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogDebug("Saved {EventCount} events for aggregate {AggregateId}", events.Count(), aggregateId);
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
        try
        {
            var eventEntities = await _context.Events
                .Where(e => e.AggregateId == aggregateId)
                .OrderBy(e => e.Version)
                .AsNoTracking()
                .ToListAsync();

            return eventEntities.Select(DeserializeEvent).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for aggregate {AggregateId}", aggregateId);
            throw;
        }
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion)
    {
        try
        {
            var eventEntities = await _context.Events
                .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
                .OrderBy(e => e.Version)
                .AsNoTracking()
                .ToListAsync();

            return eventEntities.Select(DeserializeEvent).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events for aggregate {AggregateId} from version {FromVersion}", aggregateId, fromVersion);
            throw;
        }
    }

    public async Task<IEnumerable<DomainEvent>> GetEventsByTypeAsync(string eventType, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.Events.Where(e => e.EventType == eventType);

            if (fromDate.HasValue)
                query = query.Where(e => e.OccurredAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.OccurredAt <= toDate.Value);

            var eventEntities = await query
                .OrderBy(e => e.OccurredAt)
                .AsNoTracking()
                .ToListAsync();

            return eventEntities.Select(DeserializeEvent).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get events by type {EventType}", eventType);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid aggregateId)
    {
        try
        {
            return await _context.Events.AnyAsync(e => e.AggregateId == aggregateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence for aggregate {AggregateId}", aggregateId);
            throw;
        }
    }

    private async Task<int> GetCurrentVersionAsync(Guid aggregateId)
    {
        var maxVersion = await _context.Events
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version);

        return maxVersion ?? 0;
    }

    private DomainEvent DeserializeEvent(EventEntity eventEntity)
    {
        try
        {
            var eventType = Type.GetType($"URLShortener.Core.Domain.Enhanced.{eventEntity.EventType}")
                ?? throw new InvalidOperationException($"Unknown event type: {eventEntity.EventType}");

            var domainEvent = JsonSerializer.Deserialize(eventEntity.EventData, eventType) as DomainEvent
                ?? throw new InvalidOperationException($"Failed to deserialize event: {eventEntity.EventType}");

            return domainEvent with { Version = eventEntity.Version };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize event {EventId} of type {EventType}",
                eventEntity.EventId, eventEntity.EventType);
            throw;
        }
    }
}