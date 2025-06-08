using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Data.Entities;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace URLShortener.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly UrlShortenerDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;
    private readonly ConcurrentQueue<AnalyticsEntity> _batchQueue = new();
    private readonly Timer _batchTimer;
    private readonly int _batchSize = 1000;
    private readonly TimeSpan _batchInterval = TimeSpan.FromSeconds(5);

    public AnalyticsService(UrlShortenerDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
        _batchTimer = new Timer(ProcessBatchAsync, null, _batchInterval, _batchInterval);
    }

    public async Task RecordAccessAsync(string shortCode, string ipAddress, string userAgent, string referrer, GeoLocation location, DeviceInfo deviceInfo)
    {
        try
        {
            var analyticsEntity = new AnalyticsEntity
            {
                Id = Guid.NewGuid(),
                ShortCode = shortCode,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Referrer = referrer,
                Country = location.Country,
                Region = location.Region,
                City = location.City,
                DeviceType = deviceInfo.DeviceType,
                Browser = deviceInfo.Browser,
                OperatingSystem = deviceInfo.OperatingSystem,
                Timestamp = DateTime.UtcNow
            };

            // Add to batch queue instead of immediate save
            _batchQueue.Enqueue(analyticsEntity);

            // If queue is getting large, trigger immediate batch processing
            if (_batchQueue.Count >= _batchSize)
            {
                await Task.Run(() => ProcessBatchAsync(null));
            }

            _logger.LogDebug("Queued analytics for {ShortCode} from {Country}", shortCode, location.Country);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record analytics for {ShortCode}", shortCode);
        }
    }

    public async Task RecordCacheHitAsync(string shortCode, string layer, TimeSpan responseTime)
    {
        // This could be sent to a time-series database like InfluxDB or Prometheus
        _logger.LogDebug("Cache {Layer} hit for {ShortCode} in {ResponseTime}ms",
            layer, shortCode, responseTime.TotalMilliseconds);
        await Task.CompletedTask;
    }

    public async Task RecordCacheMissAsync(string shortCode, TimeSpan responseTime)
    {
        _logger.LogDebug("Cache miss for {ShortCode} in {ResponseTime}ms",
            shortCode, responseTime.TotalMilliseconds);
        await Task.CompletedTask;
    }

    public async Task RecordSecurityEventAsync(string shortCode, string eventType)
    {
        _logger.LogWarning("Security event {EventType} for {ShortCode}", eventType, shortCode);

        // In production, you would:
        // 1. Send to security monitoring system
        // 2. Trigger alerts if needed
        // 3. Store in security event log
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<PopularUrl>> GetTopUrlsAsync(int count, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);

        var topUrls = await _context.Analytics
            .Where(a => a.Timestamp >= cutoffTime)
            .GroupBy(a => a.ShortCode)
            .Select(g => new
            {
                ShortCode = g.Key,
                AccessCount = g.Count()
            })
            .OrderByDescending(x => x.AccessCount)
            .Take(count)
            .ToListAsync();

        var result = new List<PopularUrl>();

        foreach (var item in topUrls)
        {
            var url = await _context.Urls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == item.ShortCode);

            if (url != null)
            {
                result.Add(new PopularUrl(
                    ShortCode: item.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    AccessCount: item.AccessCount,
                    TrendScore: CalculateTrendScore(item.AccessCount, timeWindow)
                ));
            }
        }

        return result;
    }

    public async Task<IEnumerable<PopularUrl>> GetTrendingUrlsAsync(int count, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
        var halfWindow = DateTime.UtcNow.Subtract(TimeSpan.FromTicks(timeWindow.Ticks / 2));

        // Get URLs with increasing access patterns
        var trendingUrls = await _context.Analytics
            .Where(a => a.Timestamp >= cutoffTime)
            .GroupBy(a => a.ShortCode)
            .Select(g => new
            {
                ShortCode = g.Key,
                TotalCount = g.Count(),
                RecentCount = g.Count(a => a.Timestamp >= halfWindow),
                TrendRatio = (double)g.Count(a => a.Timestamp >= halfWindow) / g.Count()
            })
            .Where(x => x.TrendRatio > 0.6) // URLs with 60%+ of traffic in recent half
            .OrderByDescending(x => x.TrendRatio)
            .ThenByDescending(x => x.TotalCount)
            .Take(count)
            .ToListAsync();

        var result = new List<PopularUrl>();

        foreach (var item in trendingUrls)
        {
            var url = await _context.Urls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == item.ShortCode);

            if (url != null)
            {
                result.Add(new PopularUrl(
                    ShortCode: item.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    AccessCount: item.TotalCount,
                    TrendScore: item.TrendRatio * 100
                ));
            }
        }

        return result;
    }

    public async Task<IEnumerable<PopularUrl>> GetRegionalPopularUrlsAsync(string region, int count, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);

        // Map region to countries (simplified)
        var countries = GetCountriesForRegion(region);

        var popularUrls = await _context.Analytics
            .Where(a => a.Timestamp >= cutoffTime && countries.Contains(a.Country))
            .GroupBy(a => a.ShortCode)
            .Select(g => new
            {
                ShortCode = g.Key,
                AccessCount = g.Count()
            })
            .OrderByDescending(x => x.AccessCount)
            .Take(count)
            .ToListAsync();

        var result = new List<PopularUrl>();

        foreach (var item in popularUrls)
        {
            var url = await _context.Urls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == item.ShortCode);

            if (url != null)
            {
                result.Add(new PopularUrl(
                    ShortCode: item.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    AccessCount: item.AccessCount,
                    TrendScore: CalculateTrendScore(item.AccessCount, timeWindow)
                ));
            }
        }

        return result;
    }

    public async IAsyncEnumerable<AnalyticsPoint> StreamAnalyticsAsync(
        string shortCode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AnalyticsPoint? point = null;

            try
            {
                var recentData = await GetRecentAnalyticsData(shortCode);

                point = new AnalyticsPoint(
                    Timestamp: DateTime.UtcNow,
                    AccessCount: recentData.TotalAccesses,
                    AccessRate: recentData.AccessRate,
                    Metadata: new Dictionary<string, object>
                    {
                        ["unique_countries"] = recentData.UniqueCountries,
                        ["unique_devices"] = recentData.UniqueDevices,
                        ["mobile_percentage"] = recentData.MobilePercentage
                    }
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming analytics for {ShortCode}", shortCode);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                continue;
            }

            if (point != null)
            {
                yield return point;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    public async Task<AnalyticsSummary> GetSummaryAsync(string shortCode, DateTime? startDate = null, DateTime? endDate = null)
    {
        startDate ??= DateTime.UtcNow.AddDays(-30);
        endDate ??= DateTime.UtcNow;

        var analytics = await _context.Analytics
            .Where(a => a.ShortCode == shortCode &&
                       a.Timestamp >= startDate &&
                       a.Timestamp <= endDate)
            .AsNoTracking()
            .ToListAsync();

        var totalAccesses = analytics.Count;
        var uniqueVisitors = analytics.Select(a => a.IpAddress).Distinct().Count();

        var countryBreakdown = analytics
            .GroupBy(a => a.Country)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var deviceBreakdown = analytics
            .GroupBy(a => a.DeviceType)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        // Create hourly time series data
        var timeSeriesData = analytics
            .GroupBy(a => new DateTime(a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day, a.Timestamp.Hour, 0, 0))
            .ToDictionary(g => g.Key.ToString("yyyy-MM-dd HH:00"), g => (long)g.Count());

        return new AnalyticsSummary(
            ShortCode: shortCode,
            TotalAccesses: totalAccesses,
            UniqueVisitors: uniqueVisitors,
            CountryBreakdown: countryBreakdown,
            DeviceBreakdown: deviceBreakdown,
            TimeSeriesData: timeSeriesData,
            StartDate: startDate.Value,
            EndDate: endDate.Value
        );
    }

    private async Task<RecentAnalyticsData> GetRecentAnalyticsData(string shortCode)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

        var recentAnalytics = await _context.Analytics
            .Where(a => a.ShortCode == shortCode && a.Timestamp >= cutoffTime)
            .AsNoTracking()
            .ToListAsync();

        var totalAccesses = recentAnalytics.Count;
        var accessRate = totalAccesses / 5.0; // Accesses per minute
        var uniqueCountries = recentAnalytics.Select(a => a.Country).Distinct().Count();
        var uniqueDevices = recentAnalytics.Select(a => a.DeviceType).Distinct().Count();
        var mobileCount = recentAnalytics.Count(a => a.IsMobile);
        var mobilePercentage = totalAccesses > 0 ? (mobileCount * 100.0) / totalAccesses : 0;

        return new RecentAnalyticsData(
            TotalAccesses: totalAccesses,
            AccessRate: accessRate,
            UniqueCountries: uniqueCountries,
            UniqueDevices: uniqueDevices,
            MobilePercentage: mobilePercentage
        );
    }

    private static double CalculateTrendScore(long accessCount, TimeSpan timeWindow)
    {
        // Simple trend score calculation
        var hoursInWindow = timeWindow.TotalHours;
        var accessesPerHour = accessCount / hoursInWindow;

        // Normalize to 0-100 scale (this could be more sophisticated)
        return Math.Min(100, accessesPerHour * 10);
    }

    private async void ProcessBatchAsync(object? state)
    {
        if (_batchQueue.IsEmpty)
            return;

        var batch = new List<AnalyticsEntity>();
        var processedCount = 0;

        // Dequeue up to batch size
        while (processedCount < _batchSize && _batchQueue.TryDequeue(out var entity))
        {
            batch.Add(entity);
            processedCount++;
        }

        if (batch.Count == 0)
            return;

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            // Use bulk insert for better performance
            _context.Analytics.AddRange(batch);
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();

            _logger.LogInformation("Processed batch of {Count} analytics records", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process analytics batch of {Count} records", batch.Count);
            
            // Re-queue failed items
            foreach (var item in batch)
            {
                _batchQueue.Enqueue(item);
            }
        }
    }

    private static IEnumerable<string> GetCountriesForRegion(string region)
    {
        return region.ToLowerInvariant() switch
        {
            "us-east" => new[] { "US" },
            "us-west" => new[] { "US" },
            "europe" => new[] { "GB", "DE", "FR", "IT", "ES", "NL", "PL" },
            "asia-pacific" => new[] { "JP", "KR", "SG", "AU", "IN", "CN" },
            "australia" => new[] { "AU", "NZ" },
            _ => new[] { "Unknown" }
        };
    }

    public void Dispose()
    {
        // Process any remaining items in the queue
        ProcessBatchAsync(null);
        _batchTimer?.Dispose();
    }
}

internal record RecentAnalyticsData(
    long TotalAccesses,
    double AccessRate,
    int UniqueCountries,
    int UniqueDevices,
    double MobilePercentage
);
