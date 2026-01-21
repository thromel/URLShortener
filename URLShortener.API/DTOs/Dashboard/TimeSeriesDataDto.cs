namespace URLShortener.API.DTOs.Dashboard;

/// <summary>
/// Time series data for charts with configurable intervals.
/// </summary>
public class TimeSeriesDataDto
{
    public string ShortCode { get; set; } = string.Empty;
    public string Interval { get; set; } = "hour";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TimeSeriesPointDto> Points { get; set; } = new();
    public TimeSeriesAggregateDto Aggregates { get; set; } = new();
}

/// <summary>
/// Individual time series data point.
/// </summary>
public class TimeSeriesPointDto
{
    public DateTime Timestamp { get; set; }
    public long Clicks { get; set; }
    public long UniqueVisitors { get; set; }
    public double AvgResponseTime { get; set; }
}

/// <summary>
/// Aggregate statistics for the time period.
/// </summary>
public class TimeSeriesAggregateDto
{
    public long TotalClicks { get; set; }
    public long TotalUniqueVisitors { get; set; }
    public long PeakHourClicks { get; set; }
    public DateTime PeakHour { get; set; }
    public double AvgClicksPerInterval { get; set; }
}
