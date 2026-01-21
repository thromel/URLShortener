using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using URLShortener.API.DTOs.Dashboard;
using URLShortener.API.Services;
using URLShortener.Core.Interfaces;

namespace URLShortener.API.Controllers;

/// <summary>
/// Dashboard endpoints for analytics visualization.
/// </summary>
public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(
        IDashboardService dashboardService,
        IClientInfoService clientInfoService,
        ILogger<DashboardController> logger)
        : base(clientInfoService, logger)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Gets time series data for charts.
    /// </summary>
    /// <param name="shortCode">Optional short code to filter by specific URL.</param>
    /// <param name="startDate">Start date for the time range.</param>
    /// <param name="endDate">End date for the time range.</param>
    /// <param name="interval">Time interval: minute, hour, day, week.</param>
    [HttpGet("timeseries/{shortCode?}")]
    [SwaggerOperation(Summary = "Get time series analytics data")]
    [SwaggerResponse(200, "Time series data retrieved successfully", typeof(TimeSeriesDataDto))]
    public async Task<ActionResult<TimeSeriesDataDto>> GetTimeSeries(
        string? shortCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string interval = "hour")
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;

        var data = await _dashboardService.GetTimeSeriesAsync(shortCode, start, end, interval);

        return Ok(new TimeSeriesDataDto
        {
            ShortCode = data.ShortCode ?? string.Empty,
            Interval = data.Interval,
            StartDate = data.StartDate,
            EndDate = data.EndDate,
            Points = data.Points.Select(p => new TimeSeriesPointDto
            {
                Timestamp = p.Timestamp,
                Clicks = p.Clicks,
                UniqueVisitors = p.UniqueVisitors
            }).ToList(),
            Aggregates = new TimeSeriesAggregateDto
            {
                TotalClicks = data.TotalClicks,
                TotalUniqueVisitors = data.TotalUniqueVisitors,
                PeakHourClicks = data.PeakClicks,
                PeakHour = data.PeakTime,
                AvgClicksPerInterval = data.Points.Count > 0
                    ? (double)data.TotalClicks / data.Points.Count
                    : 0
            }
        });
    }

    /// <summary>
    /// Gets dashboard overview with summary statistics.
    /// </summary>
    [HttpGet("overview")]
    [SwaggerOperation(Summary = "Get dashboard overview statistics")]
    [SwaggerResponse(200, "Overview retrieved successfully", typeof(DashboardOverviewDto))]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverview()
    {
        Guid? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            userId = GetUserId();
        }

        var data = await _dashboardService.GetOverviewAsync(userId);

        return Ok(new DashboardOverviewDto
        {
            TotalUrls = CreateOverviewStat(data.TotalUrls, data.TotalUrlsPrevious),
            TotalClicks = CreateOverviewStat(data.TotalClicks, data.TotalClicksPrevious),
            ActiveUrls = CreateOverviewStat(data.ActiveUrls, data.ActiveUrlsPrevious),
            UniqueVisitors = CreateOverviewStat(data.UniqueVisitors, data.UniqueVisitorsPrevious),
            TopUrls = data.TopUrls.Select((u, i) => new TopUrlDto
            {
                ShortCode = u.ShortCode,
                OriginalUrl = u.OriginalUrl,
                Clicks = u.Clicks,
                PercentOfTotal = data.TotalClicks > 0
                    ? Math.Round((double)u.Clicks / data.TotalClicks * 100, 2)
                    : 0
            }).ToList(),
            RecentActivity = data.RecentActivities.Select(a => new RecentActivityDto
            {
                Timestamp = a.Timestamp,
                Type = a.Type,
                ShortCode = a.ShortCode,
                Details = a.Details
            }).ToList(),
            SystemHealth = new SystemHealthDto
            {
                CacheHitRate = data.CacheHitRate,
                AvgResponseTimeMs = data.AvgResponseTimeMs,
                Status = data.CacheHitRate > 0.9 ? "healthy" : data.CacheHitRate > 0.7 ? "degraded" : "unhealthy"
            }
        });
    }

    /// <summary>
    /// Gets geographic data for heatmap visualization.
    /// </summary>
    [HttpGet("geographic")]
    [SwaggerOperation(Summary = "Get geographic analytics data")]
    [SwaggerResponse(200, "Geographic data retrieved successfully", typeof(GeographicHeatmapDto))]
    public async Task<ActionResult<GeographicHeatmapDto>> GetGeographicData(
        [FromQuery] string? shortCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var data = await _dashboardService.GetGeographicDataAsync(shortCode, startDate, endDate);

        return Ok(new GeographicHeatmapDto
        {
            ShortCode = data.ShortCode,
            StartDate = data.StartDate,
            EndDate = data.EndDate,
            TotalAccesses = data.TotalAccesses,
            CountriesReached = data.Countries.Count,
            Countries = data.Countries.Select(c => new CountryDataDto
            {
                CountryCode = c.CountryCode,
                CountryName = c.CountryName,
                Clicks = c.Clicks,
                Percentage = data.TotalAccesses > 0
                    ? Math.Round((double)c.Clicks / data.TotalAccesses * 100, 2)
                    : 0
            }).ToList(),
            TopCities = data.TopCities.Select(c => new CityDataDto
            {
                CountryCode = c.CountryCode,
                CityName = c.CityName,
                Clicks = c.Clicks,
                Percentage = data.TotalAccesses > 0
                    ? Math.Round((double)c.Clicks / data.TotalAccesses * 100, 2)
                    : 0
            }).ToList()
        });
    }

    /// <summary>
    /// Gets device breakdown data for charts.
    /// </summary>
    [HttpGet("devices")]
    [SwaggerOperation(Summary = "Get device breakdown analytics")]
    [SwaggerResponse(200, "Device data retrieved successfully", typeof(DeviceBreakdownDto))]
    public async Task<ActionResult<DeviceBreakdownDto>> GetDeviceBreakdown(
        [FromQuery] string? shortCode = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var data = await _dashboardService.GetDeviceBreakdownAsync(shortCode, startDate, endDate);
        var total = data.TotalAccesses;

        var colors = new[] { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16" };

        return Ok(new DeviceBreakdownDto
        {
            ShortCode = data.ShortCode,
            StartDate = data.StartDate,
            EndDate = data.EndDate,
            TotalAccesses = total,
            DeviceTypes = data.DeviceTypes.Select((kv, i) => new DeviceCategoryDto
            {
                Name = kv.Key,
                Count = kv.Value,
                Percentage = total > 0 ? Math.Round((double)kv.Value / total * 100, 2) : 0,
                Color = colors[i % colors.Length]
            }).OrderByDescending(d => d.Count).ToList(),
            Browsers = data.Browsers.Select((kv, i) => new DeviceCategoryDto
            {
                Name = kv.Key,
                Count = kv.Value,
                Percentage = total > 0 ? Math.Round((double)kv.Value / total * 100, 2) : 0,
                Color = colors[i % colors.Length]
            }).OrderByDescending(b => b.Count).ToList(),
            OperatingSystems = data.OperatingSystems.Select((kv, i) => new DeviceCategoryDto
            {
                Name = kv.Key,
                Count = kv.Value,
                Percentage = total > 0 ? Math.Round((double)kv.Value / total * 100, 2) : 0,
                Color = colors[i % colors.Length]
            }).OrderByDescending(o => o.Count).ToList(),
            MobileVsDesktop = new MobileVsDesktopDto
            {
                MobileCount = data.MobileCount,
                MobilePercentage = total > 0 ? Math.Round((double)data.MobileCount / total * 100, 2) : 0,
                DesktopCount = data.DesktopCount,
                DesktopPercentage = total > 0 ? Math.Round((double)data.DesktopCount / total * 100, 2) : 0,
                TabletCount = data.TabletCount,
                TabletPercentage = total > 0 ? Math.Round((double)data.TabletCount / total * 100, 2) : 0
            }
        });
    }

    /// <summary>
    /// Compares metrics between two periods.
    /// </summary>
    [HttpGet("compare")]
    [SwaggerOperation(Summary = "Compare analytics between two periods")]
    [SwaggerResponse(200, "Comparison data retrieved successfully", typeof(ComparisonDto))]
    public async Task<ActionResult<ComparisonDto>> Compare(
        [FromQuery] DateTime currentStart,
        [FromQuery] DateTime currentEnd,
        [FromQuery] DateTime previousStart,
        [FromQuery] DateTime previousEnd,
        [FromQuery] string? shortCode = null)
    {
        var data = await _dashboardService.ComparePeriodsAsync(
            currentStart, currentEnd, previousStart, previousEnd, shortCode);

        return Ok(new ComparisonDto
        {
            CurrentPeriod = new DateRangeDto
            {
                StartDate = data.CurrentStart,
                EndDate = data.CurrentEnd,
                Label = $"{data.CurrentStart:MMM dd} - {data.CurrentEnd:MMM dd}"
            },
            PreviousPeriod = new DateRangeDto
            {
                StartDate = data.PreviousStart,
                EndDate = data.PreviousEnd,
                Label = $"{data.PreviousStart:MMM dd} - {data.PreviousEnd:MMM dd}"
            },
            Metrics = new ComparisonMetricsDto
            {
                TotalClicks = CreateComparisonValue(data.CurrentClicks, data.PreviousClicks),
                UniqueVisitors = CreateComparisonValue(data.CurrentUniqueVisitors, data.PreviousUniqueVisitors),
                NewUrls = CreateComparisonValue(data.CurrentNewUrls, data.PreviousNewUrls),
                AvgClicksPerUrl = CreateComparisonValue(
                    data.CurrentNewUrls > 0 ? (double)data.CurrentClicks / data.CurrentNewUrls : 0,
                    data.PreviousNewUrls > 0 ? (double)data.PreviousClicks / data.PreviousNewUrls : 0)
            },
            Series = data.Series.Select(s => new ComparisonSeriesDto
            {
                CurrentTimestamp = s.CurrentTimestamp,
                PreviousTimestamp = s.PreviousTimestamp,
                CurrentClicks = s.CurrentClicks,
                PreviousClicks = s.PreviousClicks
            }).ToList()
        });
    }

    private static OverviewStatDto CreateOverviewStat(long value, long previousValue)
    {
        var change = previousValue > 0
            ? Math.Round((double)(value - previousValue) / previousValue * 100, 2)
            : (value > 0 ? 100 : 0);

        return new OverviewStatDto
        {
            Value = value,
            PreviousValue = previousValue,
            PercentChange = change,
            Trend = change > 0 ? "up" : change < 0 ? "down" : "stable"
        };
    }

    private static ComparisonValueDto CreateComparisonValue(double current, double previous)
    {
        var absoluteChange = current - previous;
        var percentChange = previous > 0
            ? Math.Round((current - previous) / previous * 100, 2)
            : (current > 0 ? 100 : 0);

        return new ComparisonValueDto
        {
            CurrentValue = current,
            PreviousValue = previous,
            AbsoluteChange = absoluteChange,
            PercentChange = percentChange,
            Trend = absoluteChange > 0 ? "up" : absoluteChange < 0 ? "down" : "stable"
        };
    }
}
