namespace URLShortener.API.DTOs.Dashboard;

/// <summary>
/// Device breakdown data for pie/donut charts.
/// </summary>
public class DeviceBreakdownDto
{
    public string? ShortCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalAccesses { get; set; }
    public List<DeviceCategoryDto> DeviceTypes { get; set; } = new();
    public List<DeviceCategoryDto> Browsers { get; set; } = new();
    public List<DeviceCategoryDto> OperatingSystems { get; set; } = new();
    public MobileVsDesktopDto MobileVsDesktop { get; set; } = new();
}

/// <summary>
/// Individual category breakdown.
/// </summary>
public class DeviceCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty; // Suggested color for charts
}

/// <summary>
/// Mobile vs Desktop comparison.
/// </summary>
public class MobileVsDesktopDto
{
    public long MobileCount { get; set; }
    public double MobilePercentage { get; set; }
    public long DesktopCount { get; set; }
    public double DesktopPercentage { get; set; }
    public long TabletCount { get; set; }
    public double TabletPercentage { get; set; }
}
