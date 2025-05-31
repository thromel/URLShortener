namespace URLShortener.Core.Interfaces;

public interface ICdnCache
{
    Task InvalidateAsync(string path);
    Task InvalidatePatternAsync(string pattern);
    Task<string> GetSignedUrlAsync(string path, TimeSpan expiry);
    Task<bool> ExistsAsync(string path);
    Task WarmCacheAsync(IEnumerable<string> paths);
    Task<CacheStatistics> GetStatisticsAsync();
}

public record CacheStatistics(
    long HitCount,
    long MissCount,
    double HitRate,
    long TotalRequests,
    TimeSpan AverageResponseTime
);