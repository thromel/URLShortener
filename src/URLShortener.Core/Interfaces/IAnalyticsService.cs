using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public record AnalyticsPoint(
    DateTime Timestamp,
    long AccessCount,
    double AccessRate,
    Dictionary<string, object> Metadata
);

public record PopularUrl(
    string ShortCode,
    string OriginalUrl,
    long AccessCount,
    double TrendScore
);

public record AnalyticsSummary(
    string ShortCode,
    long TotalAccesses,
    long UniqueVisitors,
    Dictionary<string, long> CountryBreakdown,
    Dictionary<string, long> DeviceBreakdown,
    Dictionary<string, long> TimeSeriesData,
    DateTime StartDate,
    DateTime EndDate
);

public interface IAnalyticsService
{
    Task RecordCacheHitAsync(string shortCode, string layer, TimeSpan responseTime);
    Task RecordCacheMissAsync(string shortCode, TimeSpan responseTime);
    Task RecordSecurityEventAsync(string shortCode, string eventType);
    Task<IEnumerable<PopularUrl>> GetTopUrlsAsync(int count, TimeSpan timeWindow);
    Task<IEnumerable<PopularUrl>> GetTrendingUrlsAsync(int count, TimeSpan timeWindow);
    Task<IEnumerable<PopularUrl>> GetRegionalPopularUrlsAsync(string region, int count, TimeSpan timeWindow);
    IAsyncEnumerable<AnalyticsPoint> StreamAnalyticsAsync(string shortCode, CancellationToken cancellationToken);
    Task<AnalyticsSummary> GetSummaryAsync(string shortCode, DateTime? startDate = null, DateTime? endDate = null);
}