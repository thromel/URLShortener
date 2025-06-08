using Hangfire;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;

namespace URLShortener.Infrastructure.BackgroundJobs;

public interface IAnalyticsProcessingJob
{
    [AutomaticRetry(Attempts = 3)]
    Task ProcessAnalyticsBatchAsync();
    
    [AutomaticRetry(Attempts = 2)]
    Task GenerateDailyReportsAsync();
    
    [AutomaticRetry(Attempts = 2)]
    Task CleanupExpiredUrlsAsync();
    
    [AutomaticRetry(Attempts = 3)]
    Task WarmPopularUrlCacheAsync();
}

public class AnalyticsProcessingJob : IAnalyticsProcessingJob
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IUrlRepository _urlRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AnalyticsProcessingJob> _logger;

    public AnalyticsProcessingJob(
        IAnalyticsService analyticsService,
        IUrlRepository urlRepository,
        ICacheService cacheService,
        ILogger<AnalyticsProcessingJob> logger)
    {
        _analyticsService = analyticsService;
        _urlRepository = urlRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task ProcessAnalyticsBatchAsync()
    {
        _logger.LogInformation("Starting analytics batch processing");
        
        try
        {
            // Process any pending analytics data
            // This would typically involve aggregating raw analytics data
            // into summaries, updating materialized views, etc.
            
            _logger.LogInformation("Analytics batch processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during analytics batch processing");
            throw;
        }
    }

    public async Task GenerateDailyReportsAsync()
    {
        _logger.LogInformation("Starting daily report generation");
        
        try
        {
            var yesterday = DateTime.UtcNow.AddDays(-1).Date;
            var today = yesterday.AddDays(1);

            // Generate daily analytics summaries
            // This could include:
            // - Top URLs by traffic
            // - Geographic distribution
            // - Device/browser statistics
            // - Performance metrics
            
            _logger.LogInformation("Daily report generation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during daily report generation");
            throw;
        }
    }

    public async Task CleanupExpiredUrlsAsync()
    {
        _logger.LogInformation("Starting cleanup of expired URLs");
        
        try
        {
            var expiredUrls = await _urlRepository.GetExpiredUrlsAsync(DateTime.UtcNow);
            var deletedCount = 0;

            foreach (var url in expiredUrls)
            {
                try
                {
                    await _urlRepository.DeleteAsync(url.ShortCode);
                    await _cacheService.InvalidateAsync(url.ShortCode, CacheInvalidationReason.UrlExpired);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete expired URL {ShortCode}", url.ShortCode);
                }
            }
            
            _logger.LogInformation("Cleanup completed. Deleted {DeletedCount} expired URLs", deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired URL cleanup");
            throw;
        }
    }

    public async Task WarmPopularUrlCacheAsync()
    {
        _logger.LogInformation("Starting cache warming for popular URLs");
        
        try
        {
            // Get popular URLs from the last 24 hours
            var popularUrls = await _analyticsService.GetTopUrlsAsync(100, TimeSpan.FromHours(24));
            var warmedCount = 0;

            foreach (var popularUrl in popularUrls)
            {
                try
                {
                    // Check if already cached
                    var cached = await _cacheService.GetOriginalUrlAsync(popularUrl.ShortCode);
                    if (string.IsNullOrEmpty(cached))
                    {
                        // Warm the cache
                        var originalUrl = await _urlRepository.GetOriginalUrlAsync(popularUrl.ShortCode);
                        if (!string.IsNullOrEmpty(originalUrl))
                        {
                            await _cacheService.SetAsync(popularUrl.ShortCode, originalUrl, TimeSpan.FromHours(2));
                            warmedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to warm cache for URL {ShortCode}", popularUrl.ShortCode);
                }
            }
            
            _logger.LogInformation("Cache warming completed. Warmed {WarmedCount} URLs", warmedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warming");
            throw;
        }
    }
}