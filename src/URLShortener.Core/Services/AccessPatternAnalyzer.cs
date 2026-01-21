using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Services;

/// <summary>
/// Analyzes URL access patterns to predict peak hours and optimize cache warming.
/// Uses time-series analysis to identify trending URLs and predict future access patterns.
/// </summary>
public interface IAccessPatternAnalyzer
{
    /// <summary>
    /// Analyzes access patterns for a URL and returns predicted peak hours.
    /// </summary>
    Task<AccessPatternAnalysis> AnalyzePatternAsync(string shortCode);

    /// <summary>
    /// Classifies URLs into popularity tiers for cache warming prioritization.
    /// </summary>
    Task<IEnumerable<UrlTierClassification>> ClassifyUrlsAsync(int maxUrls = 500);

    /// <summary>
    /// Predicts the next peak hour for a given URL based on historical patterns.
    /// </summary>
    Task<DateTime?> PredictNextPeakAsync(string shortCode);

    /// <summary>
    /// Gets recommended warming intervals for each tier.
    /// </summary>
    WarmingIntervals GetWarmingIntervals();
}

/// <summary>
/// Analysis results for URL access patterns.
/// </summary>
public record AccessPatternAnalysis(
    string ShortCode,
    int[] PeakHours,
    DayOfWeek[] PeakDays,
    double AverageHourlyRate,
    double PeakHourlyRate,
    double TrendDirection,
    AccessPatternType PatternType,
    DateTime AnalyzedAt
);

/// <summary>
/// URL classification for cache warming tiers.
/// </summary>
public record UrlTierClassification(
    string ShortCode,
    string OriginalUrl,
    CacheWarmingTier Tier,
    long RecentAccessCount,
    double TrendScore,
    DateTime LastAccessed
);

/// <summary>
/// Cache warming intervals for each tier.
/// </summary>
public record WarmingIntervals(
    TimeSpan HotTier,
    TimeSpan WarmTier,
    TimeSpan ColdTier,
    TimeSpan DefaultTtl
);

/// <summary>
/// Types of access patterns detected.
/// </summary>
public enum AccessPatternType
{
    Steady,         // Consistent access throughout the day
    BusinessHours,  // Peak during 9-5
    Evening,        // Peak during evening hours
    Weekend,        // Higher on weekends
    Viral,          // Rapidly increasing
    Declining,      // Decreasing over time
    Sporadic        // No clear pattern
}

/// <summary>
/// Cache warming priority tiers.
/// </summary>
public enum CacheWarmingTier
{
    Hot,    // Top 5% - warm every 5 minutes
    Warm,   // Top 20% - warm every 15 minutes
    Cold,   // Remaining - warm every 60 minutes
    Frozen  // Inactive - don't warm proactively
}

public class AccessPatternAnalyzer : IAccessPatternAnalyzer
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<AccessPatternAnalyzer> _logger;

    // Tier thresholds (percentile-based)
    private const double HotTierPercentile = 0.95;   // Top 5%
    private const double WarmTierPercentile = 0.80;  // Top 20%
    private const double ColdTierPercentile = 0.50;  // Top 50%

    // Default warming intervals
    private static readonly WarmingIntervals DefaultIntervals = new(
        HotTier: TimeSpan.FromMinutes(5),
        WarmTier: TimeSpan.FromMinutes(15),
        ColdTier: TimeSpan.FromMinutes(60),
        DefaultTtl: TimeSpan.FromHours(2)
    );

    public AccessPatternAnalyzer(
        IAnalyticsService analyticsService,
        ILogger<AccessPatternAnalyzer> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<AccessPatternAnalysis> AnalyzePatternAsync(string shortCode)
    {
        try
        {
            // Get summary for the last 7 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-7);
            var summary = await _analyticsService.GetSummaryAsync(shortCode, startDate, endDate);

            // Analyze time series data to find patterns
            var hourlyDistribution = AnalyzeHourlyDistribution(summary.TimeSeriesData);
            var dailyDistribution = AnalyzeDailyDistribution(summary.TimeSeriesData);
            var patternType = DeterminePatternType(hourlyDistribution, summary.TotalAccesses);
            var trendDirection = CalculateTrendDirection(summary.TimeSeriesData);

            var peakHours = hourlyDistribution
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => kv.Key)
                .ToArray();

            var peakDays = dailyDistribution
                .OrderByDescending(kv => kv.Value)
                .Take(2)
                .Select(kv => kv.Key)
                .ToArray();

            var avgHourlyRate = summary.TotalAccesses / (7.0 * 24.0);
            var peakHourlyRate = hourlyDistribution.Values.DefaultIfEmpty(0).Max();

            return new AccessPatternAnalysis(
                ShortCode: shortCode,
                PeakHours: peakHours,
                PeakDays: peakDays,
                AverageHourlyRate: avgHourlyRate,
                PeakHourlyRate: peakHourlyRate,
                TrendDirection: trendDirection,
                PatternType: patternType,
                AnalyzedAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze pattern for {ShortCode}", shortCode);

            // Return default analysis on error
            return new AccessPatternAnalysis(
                ShortCode: shortCode,
                PeakHours: Array.Empty<int>(),
                PeakDays: Array.Empty<DayOfWeek>(),
                AverageHourlyRate: 0,
                PeakHourlyRate: 0,
                TrendDirection: 0,
                PatternType: AccessPatternType.Sporadic,
                AnalyzedAt: DateTime.UtcNow
            );
        }
    }

    public async Task<IEnumerable<UrlTierClassification>> ClassifyUrlsAsync(int maxUrls = 500)
    {
        try
        {
            // Get trending URLs from the last 24 hours
            var trendingUrls = await _analyticsService.GetTrendingUrlsAsync(maxUrls, TimeSpan.FromHours(24));
            var urlList = trendingUrls.ToList();

            if (!urlList.Any())
            {
                return Enumerable.Empty<UrlTierClassification>();
            }

            // Calculate percentile thresholds
            var accessCounts = urlList.Select(u => u.AccessCount).OrderBy(x => x).ToList();
            var hotThreshold = GetPercentileValue(accessCounts, HotTierPercentile);
            var warmThreshold = GetPercentileValue(accessCounts, WarmTierPercentile);
            var coldThreshold = GetPercentileValue(accessCounts, ColdTierPercentile);

            var classifications = urlList.Select(url =>
            {
                var tier = url.AccessCount >= hotThreshold ? CacheWarmingTier.Hot
                    : url.AccessCount >= warmThreshold ? CacheWarmingTier.Warm
                    : url.AccessCount >= coldThreshold ? CacheWarmingTier.Cold
                    : CacheWarmingTier.Frozen;

                return new UrlTierClassification(
                    ShortCode: url.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    Tier: tier,
                    RecentAccessCount: url.AccessCount,
                    TrendScore: url.TrendScore,
                    LastAccessed: DateTime.UtcNow // Would come from actual data
                );
            }).ToList();

            _logger.LogDebug("Classified {Count} URLs: Hot={Hot}, Warm={Warm}, Cold={Cold}, Frozen={Frozen}",
                classifications.Count,
                classifications.Count(c => c.Tier == CacheWarmingTier.Hot),
                classifications.Count(c => c.Tier == CacheWarmingTier.Warm),
                classifications.Count(c => c.Tier == CacheWarmingTier.Cold),
                classifications.Count(c => c.Tier == CacheWarmingTier.Frozen));

            return classifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify URLs for cache warming");
            return Enumerable.Empty<UrlTierClassification>();
        }
    }

    public async Task<DateTime?> PredictNextPeakAsync(string shortCode)
    {
        var analysis = await AnalyzePatternAsync(shortCode);

        if (!analysis.PeakHours.Any())
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var currentHour = now.Hour;

        // Find the next peak hour
        var nextPeakHour = analysis.PeakHours
            .Select(h => h > currentHour ? h : h + 24)
            .OrderBy(h => h)
            .First();

        var nextPeak = now.Date.AddHours(nextPeakHour % 24);
        if (nextPeakHour >= 24)
        {
            nextPeak = nextPeak.AddDays(1);
        }

        return nextPeak;
    }

    public WarmingIntervals GetWarmingIntervals() => DefaultIntervals;

    private Dictionary<int, long> AnalyzeHourlyDistribution(Dictionary<string, long> timeSeriesData)
    {
        var hourlyDistribution = new Dictionary<int, long>();

        foreach (var kvp in timeSeriesData)
        {
            if (DateTime.TryParse(kvp.Key, out var timestamp))
            {
                var hour = timestamp.Hour;
                hourlyDistribution.TryGetValue(hour, out var current);
                hourlyDistribution[hour] = current + kvp.Value;
            }
        }

        return hourlyDistribution;
    }

    private Dictionary<DayOfWeek, long> AnalyzeDailyDistribution(Dictionary<string, long> timeSeriesData)
    {
        var dailyDistribution = new Dictionary<DayOfWeek, long>();

        foreach (var kvp in timeSeriesData)
        {
            if (DateTime.TryParse(kvp.Key, out var timestamp))
            {
                var day = timestamp.DayOfWeek;
                dailyDistribution.TryGetValue(day, out var current);
                dailyDistribution[day] = current + kvp.Value;
            }
        }

        return dailyDistribution;
    }

    private AccessPatternType DeterminePatternType(Dictionary<int, long> hourlyDistribution, long totalAccesses)
    {
        if (totalAccesses == 0)
        {
            return AccessPatternType.Sporadic;
        }

        var businessHours = Enumerable.Range(9, 8).ToHashSet(); // 9-17
        var eveningHours = Enumerable.Range(18, 5).ToHashSet(); // 18-23

        var businessHourAccesses = hourlyDistribution
            .Where(kv => businessHours.Contains(kv.Key))
            .Sum(kv => kv.Value);

        var eveningAccesses = hourlyDistribution
            .Where(kv => eveningHours.Contains(kv.Key))
            .Sum(kv => kv.Value);

        var totalHourly = hourlyDistribution.Values.Sum();
        if (totalHourly == 0) return AccessPatternType.Sporadic;

        var businessRatio = (double)businessHourAccesses / totalHourly;
        var eveningRatio = (double)eveningAccesses / totalHourly;

        // Determine pattern based on distribution
        if (businessRatio > 0.6)
            return AccessPatternType.BusinessHours;
        if (eveningRatio > 0.4)
            return AccessPatternType.Evening;

        // Check for steady distribution (low variance)
        var values = hourlyDistribution.Values.ToList();
        if (values.Any())
        {
            var avg = values.Average();
            var variance = values.Average(v => Math.Pow(v - avg, 2));
            var cv = avg > 0 ? Math.Sqrt(variance) / avg : 0;

            if (cv < 0.3)
                return AccessPatternType.Steady;
        }

        return AccessPatternType.Sporadic;
    }

    private double CalculateTrendDirection(Dictionary<string, long> timeSeriesData)
    {
        var sortedData = timeSeriesData
            .Where(kv => DateTime.TryParse(kv.Key, out _))
            .OrderBy(kv => DateTime.Parse(kv.Key))
            .ToList();

        if (sortedData.Count < 2)
        {
            return 0;
        }

        // Simple linear regression to determine trend
        var n = sortedData.Count;
        var sumX = (double)(n * (n - 1)) / 2;
        var sumY = sortedData.Sum(kv => (double)kv.Value);
        var sumXY = sortedData.Select((kv, i) => i * (double)kv.Value).Sum();
        var sumX2 = (double)(n * (n - 1) * (2 * n - 1)) / 6;

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

        // Normalize to -1 to 1 range
        var maxValue = sortedData.Max(kv => kv.Value);
        return maxValue > 0 ? Math.Clamp(slope / maxValue, -1, 1) : 0;
    }

    private static long GetPercentileValue(List<long> sortedValues, double percentile)
    {
        if (!sortedValues.Any()) return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(index, sortedValues.Count - 1));
        return sortedValues[index];
    }
}
