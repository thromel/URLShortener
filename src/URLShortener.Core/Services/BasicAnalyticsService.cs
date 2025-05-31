using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Services;

public class BasicAnalyticsService : IAnalyticsService
{
    private readonly ILogger<BasicAnalyticsService> _logger;

    public BasicAnalyticsService(ILogger<BasicAnalyticsService> logger)
    {
        _logger = logger;
    }

    public async Task RecordCacheHitAsync(string shortCode, string layer, TimeSpan responseTime)
    {
        _logger.LogDebug("Cache hit for {ShortCode} on layer {Layer}", shortCode, layer);
        await Task.CompletedTask;
    }

    public async Task RecordCacheMissAsync(string shortCode, TimeSpan responseTime)
    {
        _logger.LogDebug("Cache miss for {ShortCode}", shortCode);
        await Task.CompletedTask;
    }

    public async Task RecordSecurityEventAsync(string shortCode, string eventType)
    {
        _logger.LogWarning("Security event {EventType} for {ShortCode}", eventType, shortCode);
        await Task.CompletedTask;
    }

    public async Task<IEnumerable<PopularUrl>> GetTopUrlsAsync(int count, TimeSpan timeWindow)
    {
        // Return empty list for basic implementation
        return await Task.FromResult(new List<PopularUrl>());
    }

    public async Task<IEnumerable<PopularUrl>> GetTrendingUrlsAsync(int count, TimeSpan timeWindow)
    {
        // Return empty list for basic implementation
        return await Task.FromResult(new List<PopularUrl>());
    }

    public async Task<IEnumerable<PopularUrl>> GetRegionalPopularUrlsAsync(string region, int count, TimeSpan timeWindow)
    {
        // Return empty list for basic implementation
        return await Task.FromResult(new List<PopularUrl>());
    }

    public async IAsyncEnumerable<AnalyticsPoint> StreamAnalyticsAsync(string shortCode, CancellationToken cancellationToken)
    {
        // Basic implementation - yield a single point and stop
        yield return new AnalyticsPoint(
            Timestamp: DateTime.UtcNow,
            AccessCount: 0,
            AccessRate: 0,
            Metadata: new Dictionary<string, object>()
        );

        await Task.CompletedTask;
    }

    public async Task<AnalyticsSummary> GetSummaryAsync(string shortCode, DateTime? startDate = null, DateTime? endDate = null)
    {
        return await Task.FromResult(new AnalyticsSummary(
            ShortCode: shortCode,
            TotalAccesses: 0,
            UniqueVisitors: 0,
            CountryBreakdown: new Dictionary<string, long>(),
            DeviceBreakdown: new Dictionary<string, long>(),
            TimeSeriesData: new Dictionary<string, long>(),
            StartDate: startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate: endDate ?? DateTime.UtcNow
        ));
    }
}