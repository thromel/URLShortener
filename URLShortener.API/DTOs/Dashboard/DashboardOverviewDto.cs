namespace URLShortener.API.DTOs.Dashboard;

/// <summary>
/// Dashboard overview with summary statistics for cards.
/// </summary>
public class DashboardOverviewDto
{
    public OverviewStatDto TotalUrls { get; set; } = new();
    public OverviewStatDto TotalClicks { get; set; } = new();
    public OverviewStatDto ActiveUrls { get; set; } = new();
    public OverviewStatDto UniqueVisitors { get; set; } = new();
    public List<TopUrlDto> TopUrls { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
    public SystemHealthDto SystemHealth { get; set; } = new();
}

/// <summary>
/// Individual statistic with comparison to previous period.
/// </summary>
public class OverviewStatDto
{
    public long Value { get; set; }
    public long PreviousValue { get; set; }
    public double PercentChange { get; set; }
    public string Trend { get; set; } = "stable"; // up, down, stable
}

/// <summary>
/// Top performing URL summary.
/// </summary>
public class TopUrlDto
{
    public string ShortCode { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public double PercentOfTotal { get; set; }
}

/// <summary>
/// Recent activity entry.
/// </summary>
public class RecentActivityDto
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty; // created, clicked, expired
    public string ShortCode { get; set; } = string.Empty;
    public string? Details { get; set; }
}

/// <summary>
/// System health indicators.
/// </summary>
public class SystemHealthDto
{
    public double CacheHitRate { get; set; }
    public double AvgResponseTimeMs { get; set; }
    public int ActiveConnections { get; set; }
    public string Status { get; set; } = "healthy"; // healthy, degraded, unhealthy
}
