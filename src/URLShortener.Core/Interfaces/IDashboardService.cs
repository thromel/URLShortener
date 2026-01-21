namespace URLShortener.Core.Interfaces;

/// <summary>
/// Service for dashboard analytics with optimized queries.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Gets time series data for charts.
    /// </summary>
    Task<TimeSeriesData> GetTimeSeriesAsync(
        string? shortCode,
        DateTime startDate,
        DateTime endDate,
        string interval = "hour",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets dashboard overview statistics.
    /// </summary>
    Task<DashboardOverview> GetOverviewAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets geographic breakdown data.
    /// </summary>
    Task<GeographicData> GetGeographicDataAsync(
        string? shortCode,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets device breakdown data.
    /// </summary>
    Task<DeviceBreakdown> GetDeviceBreakdownAsync(
        string? shortCode,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares metrics between two periods.
    /// </summary>
    Task<PeriodComparison> ComparePeriodsAsync(
        DateTime currentStart,
        DateTime currentEnd,
        DateTime previousStart,
        DateTime previousEnd,
        string? shortCode = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Time series data model.
/// </summary>
public class TimeSeriesData
{
    public string? ShortCode { get; set; }
    public string Interval { get; set; } = "hour";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TimeSeriesPoint> Points { get; set; } = new();
    public long TotalClicks { get; set; }
    public long TotalUniqueVisitors { get; set; }
    public long PeakClicks { get; set; }
    public DateTime PeakTime { get; set; }
}

/// <summary>
/// Individual time series point.
/// </summary>
public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public long Clicks { get; set; }
    public long UniqueVisitors { get; set; }
}

/// <summary>
/// Dashboard overview model.
/// </summary>
public class DashboardOverview
{
    public long TotalUrls { get; set; }
    public long TotalUrlsPrevious { get; set; }
    public long TotalClicks { get; set; }
    public long TotalClicksPrevious { get; set; }
    public long ActiveUrls { get; set; }
    public long ActiveUrlsPrevious { get; set; }
    public long UniqueVisitors { get; set; }
    public long UniqueVisitorsPrevious { get; set; }
    public List<TopUrl> TopUrls { get; set; } = new();
    public List<RecentActivity> RecentActivities { get; set; } = new();
    public double CacheHitRate { get; set; }
    public double AvgResponseTimeMs { get; set; }
}

/// <summary>
/// Top URL summary.
/// </summary>
public class TopUrl
{
    public string ShortCode { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public long Clicks { get; set; }
}

/// <summary>
/// Recent activity entry.
/// </summary>
public class RecentActivity
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// Geographic data model.
/// </summary>
public class GeographicData
{
    public string? ShortCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalAccesses { get; set; }
    public List<CountryData> Countries { get; set; } = new();
    public List<CityData> TopCities { get; set; } = new();
}

/// <summary>
/// Country-level data.
/// </summary>
public class CountryData
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public long Clicks { get; set; }
}

/// <summary>
/// City-level data.
/// </summary>
public class CityData
{
    public string CountryCode { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public long Clicks { get; set; }
}

/// <summary>
/// Device breakdown model.
/// </summary>
public class DeviceBreakdown
{
    public string? ShortCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalAccesses { get; set; }
    public Dictionary<string, long> DeviceTypes { get; set; } = new();
    public Dictionary<string, long> Browsers { get; set; } = new();
    public Dictionary<string, long> OperatingSystems { get; set; } = new();
    public long MobileCount { get; set; }
    public long DesktopCount { get; set; }
    public long TabletCount { get; set; }
}

/// <summary>
/// Period comparison model.
/// </summary>
public class PeriodComparison
{
    public DateTime CurrentStart { get; set; }
    public DateTime CurrentEnd { get; set; }
    public DateTime PreviousStart { get; set; }
    public DateTime PreviousEnd { get; set; }
    public long CurrentClicks { get; set; }
    public long PreviousClicks { get; set; }
    public long CurrentUniqueVisitors { get; set; }
    public long PreviousUniqueVisitors { get; set; }
    public long CurrentNewUrls { get; set; }
    public long PreviousNewUrls { get; set; }
    public List<ComparisonPoint> Series { get; set; } = new();
}

/// <summary>
/// Comparison data point.
/// </summary>
public class ComparisonPoint
{
    public DateTime CurrentTimestamp { get; set; }
    public DateTime PreviousTimestamp { get; set; }
    public long CurrentClicks { get; set; }
    public long PreviousClicks { get; set; }
}
