using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;

namespace URLShortener.Infrastructure.Services;

public class HierarchicalCacheService : ICacheService
{
    private readonly IMemoryCache _l1Cache;
    private readonly IDistributedCache _l2Cache;
    private readonly ICdnCache? _l3Cache;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<HierarchicalCacheService> _logger;

    private readonly TimeSpan _l1CacheExpiry = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _l2CacheExpiry = TimeSpan.FromHours(1);
    private readonly TimeSpan _l3CacheExpiry = TimeSpan.FromHours(24);

    public HierarchicalCacheService(
        IMemoryCache l1Cache,
        IDistributedCache l2Cache,
        IAnalyticsService analyticsService,
        ILogger<HierarchicalCacheService> logger,
        ICdnCache? l3Cache = null)
    {
        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _l3Cache = l3Cache;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Try L1 cache first (memory cache)
            if (_l1Cache.TryGetValue($"url:{shortCode}", out string? l1Value))
            {
                stopwatch.Stop();
                await _analyticsService.RecordCacheHitAsync(shortCode, "L1", stopwatch.Elapsed);
                _logger.LogDebug("L1 cache hit for {ShortCode}", shortCode);
                return l1Value;
            }

            // Try L2 cache (distributed cache)
            var l2Value = await _l2Cache.GetStringAsync($"url:{shortCode}");
            if (!string.IsNullOrEmpty(l2Value))
            {
                // Promote to L1 cache
                _l1Cache.Set($"url:{shortCode}", l2Value, _l1CacheExpiry);

                stopwatch.Stop();
                await _analyticsService.RecordCacheHitAsync(shortCode, "L2", stopwatch.Elapsed);
                _logger.LogDebug("L2 cache hit for {ShortCode}", shortCode);
                return l2Value;
            }

            // L3 cache (CDN) is typically handled at the edge level
            // and doesn't provide direct read access from application layer

            stopwatch.Stop();
            await _analyticsService.RecordCacheMissAsync(shortCode, stopwatch.Elapsed);
            _logger.LogDebug("Cache miss for {ShortCode}", shortCode);
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error retrieving from cache for {ShortCode}", shortCode);
            return null;
        }
    }

    public async Task SetAsync(string shortCode, string originalUrl, TimeSpan? expiry = null)
    {
        try
        {
            var l1Expiry = expiry ?? _l1CacheExpiry;
            var l2Expiry = expiry ?? _l2CacheExpiry;
            var l3Expiry = expiry ?? _l3CacheExpiry;

            // Set in all cache layers
            _l1Cache.Set($"url:{shortCode}", originalUrl, l1Expiry);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = l2Expiry
            };

            await _l2Cache.SetStringAsync($"url:{shortCode}", originalUrl, options);

            // CDN cache is typically managed through cache headers
            // and doesn't require explicit setting from application layer

            _logger.LogDebug("Cached URL {ShortCode} in L1 and L2 cache layers", shortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for {ShortCode}", shortCode);
        }
    }

    public async Task InvalidateAsync(string shortCode, CacheInvalidationReason reason)
    {
        try
        {
            // Remove from all cache layers
            _l1Cache.Remove($"url:{shortCode}");
            await _l2Cache.RemoveAsync($"url:{shortCode}");
            
            // Invalidate L3 cache (CDN) if available
            if (_l3Cache != null)
            {
                await _l3Cache.InvalidateAsync(shortCode);
            }

            _logger.LogInformation("Invalidated cache for {ShortCode} in all layers, reason: {Reason}", shortCode, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for {ShortCode}", shortCode);
        }
    }

    public async Task<bool> ExistsAsync(string shortCode)
    {
        try
        {
            // Check L1 first
            if (_l1Cache.TryGetValue($"url:{shortCode}", out _))
            {
                return true;
            }

            // Check L2
            var l2Value = await _l2Cache.GetStringAsync($"url:{shortCode}");
            return !string.IsNullOrEmpty(l2Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for {ShortCode}", shortCode);
            return false;
        }
    }

    public async Task InvalidatePatternAsync(string pattern)
    {
        try
        {
            // For memory cache, we can't easily invalidate by pattern
            // In production, you might use Redis SCAN with pattern
            _logger.LogWarning("Pattern invalidation not fully implemented for pattern: {Pattern}", pattern);

            // Invalidate CDN pattern if available
            if (_l3Cache != null)
            {
                await _l3Cache.InvalidatePatternAsync(pattern);
            }

            // This is a simplified implementation
            // In a real scenario, you'd need to track keys or use Redis pattern matching
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache pattern {Pattern}", pattern);
        }
    }
}