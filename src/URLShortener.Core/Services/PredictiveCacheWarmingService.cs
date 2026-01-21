using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using URLShortener.Core.Interfaces;
using System.Collections.Concurrent;

namespace URLShortener.Core.Services;

/// <summary>
/// Enhanced predictive cache warming service with tiered warming intervals.
/// Hot URLs are warmed every 5 minutes, warm URLs every 15 minutes, cold URLs every 60 minutes.
/// </summary>
public class PredictiveCacheWarmingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PredictiveCacheWarmingService> _logger;

    // Track when each tier was last warmed
    private DateTime _lastHotWarm = DateTime.MinValue;
    private DateTime _lastWarmWarm = DateTime.MinValue;
    private DateTime _lastColdWarm = DateTime.MinValue;
    private DateTime _lastClassification = DateTime.MinValue;

    // Cached URL classifications (refreshed periodically)
    private ConcurrentDictionary<string, UrlTierClassification> _urlClassifications = new();

    // Configuration
    private static readonly TimeSpan ClassificationRefreshInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan BaseCheckInterval = TimeSpan.FromMinutes(1);

    public PredictiveCacheWarmingService(
        IServiceProvider serviceProvider,
        ILogger<PredictiveCacheWarmingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Enhanced predictive cache warming service started with tiered warming");

        // Initial classification
        await RefreshClassificationsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Refresh classifications periodically
                if (now - _lastClassification > ClassificationRefreshInterval)
                {
                    await RefreshClassificationsAsync();
                }

                // Get warming intervals from analyzer
                using var scope = _serviceProvider.CreateScope();
                var analyzer = scope.ServiceProvider.GetService<IAccessPatternAnalyzer>();
                var intervals = analyzer?.GetWarmingIntervals() ?? new WarmingIntervals(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(60),
                    TimeSpan.FromHours(2)
                );

                // Warm each tier based on its interval
                var tasks = new List<Task>();

                if (now - _lastHotWarm > intervals.HotTier)
                {
                    tasks.Add(WarmTierAsync(CacheWarmingTier.Hot, intervals.DefaultTtl));
                    _lastHotWarm = now;
                }

                if (now - _lastWarmWarm > intervals.WarmTier)
                {
                    tasks.Add(WarmTierAsync(CacheWarmingTier.Warm, intervals.DefaultTtl));
                    _lastWarmWarm = now;
                }

                if (now - _lastColdWarm > intervals.ColdTier)
                {
                    tasks.Add(WarmTierAsync(CacheWarmingTier.Cold, intervals.DefaultTtl));
                    _lastColdWarm = now;
                }

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }

                await Task.Delay(BaseCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in predictive cache warming service");
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        _logger.LogInformation("Predictive cache warming service stopped");
    }

    private async Task RefreshClassificationsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var analyzer = scope.ServiceProvider.GetService<IAccessPatternAnalyzer>();

            if (analyzer == null)
            {
                _logger.LogWarning("AccessPatternAnalyzer not available, using fallback classification");
                await FallbackClassificationAsync();
                return;
            }

            var classifications = await analyzer.ClassifyUrlsAsync(500);
            var newClassifications = new ConcurrentDictionary<string, UrlTierClassification>();

            foreach (var classification in classifications)
            {
                newClassifications[classification.ShortCode] = classification;
            }

            _urlClassifications = newClassifications;
            _lastClassification = DateTime.UtcNow;

            var tierCounts = _urlClassifications.Values
                .GroupBy(c => c.Tier)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "Refreshed URL classifications: Hot={Hot}, Warm={Warm}, Cold={Cold}, Frozen={Frozen}",
                tierCounts.GetValueOrDefault(CacheWarmingTier.Hot),
                tierCounts.GetValueOrDefault(CacheWarmingTier.Warm),
                tierCounts.GetValueOrDefault(CacheWarmingTier.Cold),
                tierCounts.GetValueOrDefault(CacheWarmingTier.Frozen));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh URL classifications");
        }
    }

    private async Task FallbackClassificationAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

            // Get trending URLs and classify based on simple thresholds
            var trendingUrls = await analyticsService.GetTrendingUrlsAsync(200, TimeSpan.FromHours(24));
            var urlList = trendingUrls.ToList();

            var newClassifications = new ConcurrentDictionary<string, UrlTierClassification>();
            var topCount = Math.Max(1, urlList.Count / 20); // Top 5%
            var warmCount = Math.Max(1, urlList.Count / 5);  // Top 20%

            for (int i = 0; i < urlList.Count; i++)
            {
                var url = urlList[i];
                var tier = i < topCount ? CacheWarmingTier.Hot
                    : i < warmCount ? CacheWarmingTier.Warm
                    : CacheWarmingTier.Cold;

                newClassifications[url.ShortCode] = new UrlTierClassification(
                    url.ShortCode,
                    url.OriginalUrl,
                    tier,
                    url.AccessCount,
                    url.TrendScore,
                    DateTime.UtcNow
                );
            }

            _urlClassifications = newClassifications;
            _lastClassification = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform fallback classification");
        }
    }

    private async Task WarmTierAsync(CacheWarmingTier tier, TimeSpan ttl)
    {
        var urlsToWarm = _urlClassifications.Values
            .Where(c => c.Tier == tier)
            .ToList();

        if (!urlsToWarm.Any())
        {
            return;
        }

        _logger.LogDebug("Warming {Count} URLs in {Tier} tier", urlsToWarm.Count, tier);

        using var scope = _serviceProvider.CreateScope();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var urlRepository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

        var warmedCount = 0;
        var skippedCount = 0;

        foreach (var url in urlsToWarm)
        {
            try
            {
                // Check if URL is already cached
                var cachedUrl = await cacheService.GetOriginalUrlAsync(url.ShortCode);
                if (!string.IsNullOrEmpty(cachedUrl))
                {
                    skippedCount++;
                    continue;
                }

                // Get original URL from repository and cache it
                var originalUrl = await urlRepository.GetOriginalUrlAsync(url.ShortCode);
                if (!string.IsNullOrEmpty(originalUrl))
                {
                    // Use tier-specific TTL
                    var tierTtl = tier switch
                    {
                        CacheWarmingTier.Hot => ttl * 2,      // Hot URLs get longer TTL
                        CacheWarmingTier.Warm => ttl,
                        CacheWarmingTier.Cold => ttl / 2,     // Cold URLs get shorter TTL
                        _ => ttl
                    };

                    await cacheService.SetAsync(url.ShortCode, originalUrl, tierTtl);
                    warmedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm cache for URL {ShortCode}", url.ShortCode);
            }
        }

        if (warmedCount > 0 || _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogInformation(
                "Cache warming completed for {Tier} tier: warmed={Warmed}, skipped={Skipped} (already cached)",
                tier, warmedCount, skippedCount);
        }
    }

    /// <summary>
    /// Gets the current tier classification for a URL.
    /// </summary>
    public CacheWarmingTier? GetUrlTier(string shortCode)
    {
        return _urlClassifications.TryGetValue(shortCode, out var classification)
            ? classification.Tier
            : null;
    }

    /// <summary>
    /// Gets cache warming statistics.
    /// </summary>
    public CacheWarmingStats GetStats()
    {
        var tierCounts = _urlClassifications.Values
            .GroupBy(c => c.Tier)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CacheWarmingStats(
            TotalClassifiedUrls: _urlClassifications.Count,
            HotUrls: tierCounts.GetValueOrDefault(CacheWarmingTier.Hot),
            WarmUrls: tierCounts.GetValueOrDefault(CacheWarmingTier.Warm),
            ColdUrls: tierCounts.GetValueOrDefault(CacheWarmingTier.Cold),
            FrozenUrls: tierCounts.GetValueOrDefault(CacheWarmingTier.Frozen),
            LastClassification: _lastClassification,
            LastHotWarm: _lastHotWarm,
            LastWarmWarm: _lastWarmWarm,
            LastColdWarm: _lastColdWarm
        );
    }
}

/// <summary>
/// Statistics for cache warming operations.
/// </summary>
public record CacheWarmingStats(
    int TotalClassifiedUrls,
    int HotUrls,
    int WarmUrls,
    int ColdUrls,
    int FrozenUrls,
    DateTime LastClassification,
    DateTime LastHotWarm,
    DateTime LastWarmWarm,
    DateTime LastColdWarm
);
