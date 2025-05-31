using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.Infrastructure.Repositories;

public class UrlRepository : IUrlRepository
{
    private readonly UrlShortenerDbContext _context;
    private readonly ILogger<UrlRepository> _logger;

    public UrlRepository(UrlShortenerDbContext context, ILogger<UrlRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        var entity = await _context.Urls
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode && u.Status == UrlStatus.Active);

        if (entity?.ExpiresAt.HasValue == true && entity.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return null; // URL has expired
        }

        return entity?.OriginalUrl;
    }

    public async Task<ShortUrlAggregate?> GetByIdAsync(Guid id)
    {
        var entity = await _context.Urls
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        return entity?.ToAggregate();
    }

    public async Task<ShortUrlAggregate?> GetByShortCodeAsync(string shortCode)
    {
        var entity = await _context.Urls
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode);

        if (entity == null)
        {
            return null;
        }

        // For full event sourcing, we would reconstruct from events
        // This is a simplified version using the read model
        return CreateAggregateFromEntity(entity);
    }

    public async Task SaveAsync(ShortUrlAggregate aggregate)
    {
        var existingEntity = await _context.Urls
            .FirstOrDefaultAsync(u => u.Id == aggregate.Id);

        if (existingEntity == null)
        {
            // New aggregate
            var newEntity = UrlEntity.FromAggregate(aggregate);
            _context.Urls.Add(newEntity);
        }
        else
        {
            // Update existing
            existingEntity.ShortCode = aggregate.ShortCode;
            existingEntity.OriginalUrl = aggregate.OriginalUrl;
            existingEntity.Status = aggregate.Status;
            existingEntity.AccessCount = aggregate.AccessCount;
            existingEntity.LastAccessedAt = aggregate.LastAccessedAt;
            existingEntity.Version = aggregate.Version;

            _context.Urls.Update(existingEntity);
        }

        await _context.SaveChangesAsync();
        _logger.LogDebug("Saved aggregate {AggregateId} with version {Version}",
            aggregate.Id, aggregate.Version);
    }

    public async Task DeleteAsync(string shortCode)
    {
        var entity = await _context.Urls
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode);

        if (entity != null)
        {
            entity.Status = UrlStatus.Disabled;
            _context.Urls.Update(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<ShortUrlAggregate>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 50)
    {
        var entities = await _context.Urls
            .AsNoTracking()
            .Where(u => u.CreatedBy == userId)
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return entities.Select(CreateAggregateFromEntity);
    }

    public async Task<bool> ExistsAsync(string shortCode)
    {
        return await _context.Urls
            .AsNoTracking()
            .AnyAsync(u => u.ShortCode == shortCode);
    }

    public async Task<long> GetTotalCountAsync()
    {
        return await _context.Urls
            .AsNoTracking()
            .CountAsync();
    }

    public async Task<IEnumerable<ShortUrlAggregate>> GetExpiredUrlsAsync(DateTime cutoffDate)
    {
        var entities = await _context.Urls
            .AsNoTracking()
            .Where(u => u.ExpiresAt.HasValue && u.ExpiresAt.Value <= cutoffDate)
            .ToListAsync();

        return entities.Select(CreateAggregateFromEntity);
    }

    public async Task<IEnumerable<ShortUrlAggregate>> SearchAsync(string searchTerm, int skip = 0, int take = 50)
    {
        var entities = await _context.Urls
            .AsNoTracking()
            .Where(u => u.OriginalUrl.Contains(searchTerm) || u.ShortCode.Contains(searchTerm))
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return entities.Select(CreateAggregateFromEntity);
    }

    private static ShortUrlAggregate CreateAggregateFromEntity(UrlEntity entity)
    {
        // In a full event sourcing implementation, we would reconstruct from events
        // This is a simplified approach for the demo

        // Create events that would represent the current state
        var createEvent = new UrlCreatedEvent(
            AggregateId: entity.Id,
            OccurredAt: entity.CreatedAt,
            EventId: Guid.NewGuid(),
            ShortCode: entity.ShortCode,
            OriginalUrl: entity.OriginalUrl,
            CustomAlias: null, // We'd need to store this separately
            UserId: entity.CreatedBy,
            IpAddress: "", // We'd need to store this separately
            UserAgent: "", // We'd need to store this separately
            ExpiresAt: entity.ExpiresAt,
            Metadata: entity.Metadata
        )
        { Version = 1 };

        var events = new List<DomainEvent> { createEvent };

        // Add access events based on access count (simplified)
        for (int i = 0; i < entity.AccessCount; i++)
        {
            events.Add(new UrlAccessedEvent(
                AggregateId: entity.Id,
                OccurredAt: entity.LastAccessedAt ?? entity.CreatedAt,
                EventId: Guid.NewGuid(),
                ShortCode: entity.ShortCode,
                IpAddress: "",
                UserAgent: "",
                Referrer: "",
                Location: new GeoLocation("Unknown", "Unknown", "Unknown", 0, 0),
                DeviceInfo: new DeviceInfo("Unknown", "Unknown", "Unknown", false)
            )
            { Version = i + 2 });
        }

        return ShortUrlAggregate.FromEvents(events);
    }
}