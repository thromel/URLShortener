namespace URLShortener.API.DTOs.Dashboard;

/// <summary>
/// Period-over-period comparison data.
/// </summary>
public class ComparisonDto
{
    public DateRangeDto CurrentPeriod { get; set; } = new();
    public DateRangeDto PreviousPeriod { get; set; } = new();
    public ComparisonMetricsDto Metrics { get; set; } = new();
    public List<ComparisonSeriesDto> Series { get; set; } = new();
}

/// <summary>
/// Date range specification.
/// </summary>
public class DateRangeDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Comparison metrics between periods.
/// </summary>
public class ComparisonMetricsDto
{
    public ComparisonValueDto TotalClicks { get; set; } = new();
    public ComparisonValueDto UniqueVisitors { get; set; } = new();
    public ComparisonValueDto NewUrls { get; set; } = new();
    public ComparisonValueDto AvgClicksPerUrl { get; set; } = new();
}

/// <summary>
/// Individual comparison value.
/// </summary>
public class ComparisonValueDto
{
    public double CurrentValue { get; set; }
    public double PreviousValue { get; set; }
    public double AbsoluteChange { get; set; }
    public double PercentChange { get; set; }
    public string Trend { get; set; } = "stable"; // up, down, stable
}

/// <summary>
/// Time series comparison between periods.
/// </summary>
public class ComparisonSeriesDto
{
    public DateTime CurrentTimestamp { get; set; }
    public DateTime PreviousTimestamp { get; set; }
    public long CurrentClicks { get; set; }
    public long PreviousClicks { get; set; }
}
