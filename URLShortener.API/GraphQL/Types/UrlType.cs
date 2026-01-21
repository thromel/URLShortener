using URLShortener.Core.Interfaces;

namespace URLShortener.API.GraphQL.Types;

/// <summary>
/// GraphQL type representing a shortened URL with its statistics.
/// </summary>
public class ShortUrlType
{
    public string ShortCode { get; set; } = string.Empty;
    public string ShortUrl { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public long AccessCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? UserId { get; set; }

    /// <summary>
    /// Creates a ShortUrlType from UrlStatistics domain record.
    /// </summary>
    public static ShortUrlType FromStatistics(UrlStatistics stats, string baseUrl = "https://short.ly")
    {
        return new ShortUrlType
        {
            ShortCode = stats.ShortCode,
            ShortUrl = $"{baseUrl}/{stats.ShortCode}",
            OriginalUrl = stats.OriginalUrl,
            AccessCount = stats.AccessCount,
            CreatedAt = stats.CreatedAt,
            LastAccessedAt = stats.LastAccessedAt,
            ExpiresAt = stats.ExpiresAt,
            Status = stats.Status.ToString()
        };
    }
}

/// <summary>
/// GraphQL type for URL analytics breakdown.
/// </summary>
public class UrlAnalyticsType
{
    public string ShortCode { get; set; } = string.Empty;
    public long TotalAccesses { get; set; }
    public long UniqueVisitors { get; set; }
    public List<CountryStatType> CountryStats { get; set; } = new();
    public List<DeviceStatType> DeviceStats { get; set; } = new();
    public List<TimeSeriesPointType> TimeSeries { get; set; } = new();

    public static UrlAnalyticsType FromSummary(AnalyticsSummary summary)
    {
        return new UrlAnalyticsType
        {
            ShortCode = summary.ShortCode,
            TotalAccesses = summary.TotalAccesses,
            UniqueVisitors = summary.UniqueVisitors,
            CountryStats = summary.CountryBreakdown
                .Select(kv => new CountryStatType { Country = kv.Key, Count = kv.Value })
                .OrderByDescending(c => c.Count)
                .ToList(),
            DeviceStats = summary.DeviceBreakdown
                .Select(kv => new DeviceStatType { Device = kv.Key, Count = kv.Value })
                .OrderByDescending(d => d.Count)
                .ToList(),
            TimeSeries = summary.TimeSeriesData
                .Select(kv => new TimeSeriesPointType
                {
                    Timestamp = DateTime.TryParse(kv.Key, out var ts) ? ts : DateTime.UtcNow,
                    Count = kv.Value
                })
                .OrderBy(t => t.Timestamp)
                .ToList()
        };
    }
}

/// <summary>
/// Country statistics entry.
/// </summary>
public class CountryStatType
{
    public string Country { get; set; } = string.Empty;
    public long Count { get; set; }
}

/// <summary>
/// Device statistics entry.
/// </summary>
public class DeviceStatType
{
    public string Device { get; set; } = string.Empty;
    public long Count { get; set; }
}

/// <summary>
/// Time series data point.
/// </summary>
public class TimeSeriesPointType
{
    public DateTime Timestamp { get; set; }
    public long Count { get; set; }
}

/// <summary>
/// Input type for creating a new short URL.
/// </summary>
public class CreateUrlInput
{
    public string OriginalUrl { get; set; } = string.Empty;
    public string? CustomAlias { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Result type for URL creation mutation.
/// </summary>
public class CreateUrlPayload
{
    public ShortUrlType? Url { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Url != null && !Errors.Any();
}

/// <summary>
/// Result type for URL deletion mutation.
/// </summary>
public class DeleteUrlPayload
{
    public string? ShortCode { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Input type for updating a URL.
/// </summary>
public class UpdateUrlInput
{
    public string ShortCode { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool? IsDisabled { get; set; }
}

/// <summary>
/// Pagination input for list queries.
/// </summary>
public class PaginationInput
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}
