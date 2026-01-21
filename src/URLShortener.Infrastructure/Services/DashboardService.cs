using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Infrastructure.Data;

namespace URLShortener.Infrastructure.Services;

/// <summary>
/// Dashboard service with optimized queries and caching.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly UrlShortenerDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public DashboardService(
        UrlShortenerDbContext context,
        IMemoryCache cache,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TimeSeriesData> GetTimeSeriesAsync(
        string? shortCode,
        DateTime startDate,
        DateTime endDate,
        string interval = "hour",
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"timeseries:{shortCode ?? "all"}:{startDate:yyyyMMddHH}:{endDate:yyyyMMddHH}:{interval}";

        if (_cache.TryGetValue(cacheKey, out TimeSeriesData? cached) && cached != null)
        {
            return cached;
        }

        var query = _context.Analytics
            .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate);

        if (!string.IsNullOrEmpty(shortCode))
        {
            query = query.Where(a => a.ShortCode == shortCode);
        }

        var groupedData = interval.ToLowerInvariant() switch
        {
            "minute" => await query
                .GroupBy(a => new { a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day, a.Timestamp.Hour, a.Timestamp.Minute })
                .Select(g => new
                {
                    Timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Minute, 0),
                    Clicks = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Timestamp)
                .ToListAsync(cancellationToken),

            "day" => await query
                .GroupBy(a => new { a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day })
                .Select(g => new
                {
                    Timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day),
                    Clicks = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Timestamp)
                .ToListAsync(cancellationToken),

            "week" => await query
                .GroupBy(a => new { a.Timestamp.Year, WeekNum = (a.Timestamp.DayOfYear - 1) / 7 })
                .Select(g => new
                {
                    Timestamp = g.Min(x => x.Timestamp.Date),
                    Clicks = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Timestamp)
                .ToListAsync(cancellationToken),

            _ => await query // hour is default
                .GroupBy(a => new { a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day, a.Timestamp.Hour })
                .Select(g => new
                {
                    Timestamp = new DateTime(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, 0, 0),
                    Clicks = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Timestamp)
                .ToListAsync(cancellationToken)
        };

        var points = groupedData.Select(g => new TimeSeriesPoint
        {
            Timestamp = g.Timestamp,
            Clicks = g.Clicks,
            UniqueVisitors = g.UniqueVisitors
        }).ToList();

        var peakPoint = points.OrderByDescending(p => p.Clicks).FirstOrDefault();

        var result = new TimeSeriesData
        {
            ShortCode = shortCode,
            Interval = interval,
            StartDate = startDate,
            EndDate = endDate,
            Points = points,
            TotalClicks = points.Sum(p => p.Clicks),
            TotalUniqueVisitors = await query.Select(a => a.IpAddress).Distinct().CountAsync(cancellationToken),
            PeakClicks = peakPoint?.Clicks ?? 0,
            PeakTime = peakPoint?.Timestamp ?? DateTime.UtcNow
        };

        _cache.Set(cacheKey, result, _cacheExpiration);

        return result;
    }

    public async Task<DashboardOverview> GetOverviewAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"dashboard:overview:{userId?.ToString() ?? "all"}";

        if (_cache.TryGetValue(cacheKey, out DashboardOverview? cached) && cached != null)
        {
            return cached;
        }

        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        // Get URL counts
        var urlQuery = _context.Urls.AsQueryable();
        if (userId.HasValue)
        {
            urlQuery = urlQuery.Where(u => u.CreatedBy == userId.Value);
        }

        var totalUrls = await urlQuery.CountAsync(cancellationToken);
        var activeUrls = await urlQuery.Where(u => u.Status == UrlStatus.Active).CountAsync(cancellationToken);

        // Previous period counts (for comparison)
        var totalUrlsPrevious = await urlQuery
            .Where(u => u.CreatedAt <= dayAgo)
            .CountAsync(cancellationToken);
        var activeUrlsPrevious = await urlQuery
            .Where(u => u.Status == UrlStatus.Active && u.CreatedAt <= dayAgo)
            .CountAsync(cancellationToken);

        // Get click counts
        var shortCodes = await urlQuery.Select(u => u.ShortCode).ToListAsync(cancellationToken);
        var analyticsQuery = _context.Analytics
            .Where(a => shortCodes.Contains(a.ShortCode));

        var totalClicks = await analyticsQuery.CountAsync(cancellationToken);
        var totalClicksPrevious = await analyticsQuery
            .Where(a => a.Timestamp <= dayAgo)
            .CountAsync(cancellationToken);

        var uniqueVisitors = await analyticsQuery
            .Select(a => a.IpAddress)
            .Distinct()
            .CountAsync(cancellationToken);
        var uniqueVisitorsPrevious = await analyticsQuery
            .Where(a => a.Timestamp <= dayAgo)
            .Select(a => a.IpAddress)
            .Distinct()
            .CountAsync(cancellationToken);

        // Top URLs
        var topUrls = await analyticsQuery
            .GroupBy(a => a.ShortCode)
            .Select(g => new { ShortCode = g.Key, Clicks = g.Count() })
            .OrderByDescending(x => x.Clicks)
            .Take(10)
            .ToListAsync(cancellationToken);

        var topUrlDetails = new List<TopUrl>();
        foreach (var top in topUrls)
        {
            var url = await _context.Urls.FirstOrDefaultAsync(u => u.ShortCode == top.ShortCode, cancellationToken);
            if (url != null)
            {
                topUrlDetails.Add(new TopUrl
                {
                    ShortCode = top.ShortCode,
                    OriginalUrl = url.OriginalUrl,
                    Clicks = top.Clicks
                });
            }
        }

        // Recent activity
        var recentCreations = await urlQuery
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new RecentActivity
            {
                Timestamp = u.CreatedAt,
                Type = "created",
                ShortCode = u.ShortCode,
                Details = u.OriginalUrl
            })
            .ToListAsync(cancellationToken);

        var recentClicks = await analyticsQuery
            .OrderByDescending(a => a.Timestamp)
            .Take(5)
            .Select(a => new RecentActivity
            {
                Timestamp = a.Timestamp,
                Type = "clicked",
                ShortCode = a.ShortCode,
                Details = $"From {a.Country ?? "Unknown"}"
            })
            .ToListAsync(cancellationToken);

        var recentActivities = recentCreations
            .Concat(recentClicks)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToList();

        var result = new DashboardOverview
        {
            TotalUrls = totalUrls,
            TotalUrlsPrevious = totalUrlsPrevious,
            TotalClicks = totalClicks,
            TotalClicksPrevious = totalClicksPrevious,
            ActiveUrls = activeUrls,
            ActiveUrlsPrevious = activeUrlsPrevious,
            UniqueVisitors = uniqueVisitors,
            UniqueVisitorsPrevious = uniqueVisitorsPrevious,
            TopUrls = topUrlDetails,
            RecentActivities = recentActivities,
            CacheHitRate = 0.95, // Would be retrieved from metrics service
            AvgResponseTimeMs = 25.0 // Would be retrieved from metrics service
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));

        return result;
    }

    public async Task<GeographicData> GetGeographicDataAsync(
        string? shortCode,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        var cacheKey = $"geographic:{shortCode ?? "all"}:{start:yyyyMMdd}:{end:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out GeographicData? cached) && cached != null)
        {
            return cached;
        }

        var query = _context.Analytics
            .Where(a => a.Timestamp >= start && a.Timestamp <= end);

        if (!string.IsNullOrEmpty(shortCode))
        {
            query = query.Where(a => a.ShortCode == shortCode);
        }

        var countryData = await query
            .Where(a => a.Country != null)
            .GroupBy(a => a.Country!)
            .Select(g => new CountryData
            {
                CountryCode = g.Key,
                CountryName = g.Key, // Would need a lookup table for full names
                Clicks = g.Count()
            })
            .OrderByDescending(c => c.Clicks)
            .Take(50)
            .ToListAsync(cancellationToken);

        var cityData = await query
            .Where(a => a.City != null && a.Country != null)
            .GroupBy(a => new { a.Country, a.City })
            .Select(g => new CityData
            {
                CountryCode = g.Key.Country!,
                CityName = g.Key.City!,
                Clicks = g.Count()
            })
            .OrderByDescending(c => c.Clicks)
            .Take(20)
            .ToListAsync(cancellationToken);

        var result = new GeographicData
        {
            ShortCode = shortCode,
            StartDate = start,
            EndDate = end,
            TotalAccesses = await query.CountAsync(cancellationToken),
            Countries = countryData,
            TopCities = cityData
        };

        _cache.Set(cacheKey, result, _cacheExpiration);

        return result;
    }

    public async Task<DeviceBreakdown> GetDeviceBreakdownAsync(
        string? shortCode,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        var cacheKey = $"devices:{shortCode ?? "all"}:{start:yyyyMMdd}:{end:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out DeviceBreakdown? cached) && cached != null)
        {
            return cached;
        }

        var query = _context.Analytics
            .Where(a => a.Timestamp >= start && a.Timestamp <= end);

        if (!string.IsNullOrEmpty(shortCode))
        {
            query = query.Where(a => a.ShortCode == shortCode);
        }

        var deviceTypes = await query
            .Where(a => a.DeviceType != null)
            .GroupBy(a => a.DeviceType!)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => (long)x.Count, cancellationToken);

        var browsers = await query
            .Where(a => a.Browser != null)
            .GroupBy(a => a.Browser!)
            .Select(g => new { Browser = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Browser, x => (long)x.Count, cancellationToken);

        var operatingSystems = await query
            .Where(a => a.OperatingSystem != null)
            .GroupBy(a => a.OperatingSystem!)
            .Select(g => new { OS = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OS, x => (long)x.Count, cancellationToken);

        // Calculate mobile vs desktop
        var mobileKeywords = new[] { "Mobile", "Phone", "iOS", "Android" };
        var tabletKeywords = new[] { "Tablet", "iPad" };

        long mobileCount = deviceTypes
            .Where(kv => mobileKeywords.Any(k => kv.Key.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Sum(kv => kv.Value);

        long tabletCount = deviceTypes
            .Where(kv => tabletKeywords.Any(k => kv.Key.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Sum(kv => kv.Value);

        long totalDevices = deviceTypes.Values.Sum();
        long desktopCount = totalDevices - mobileCount - tabletCount;

        var result = new DeviceBreakdown
        {
            ShortCode = shortCode,
            StartDate = start,
            EndDate = end,
            TotalAccesses = await query.CountAsync(cancellationToken),
            DeviceTypes = deviceTypes,
            Browsers = browsers,
            OperatingSystems = operatingSystems,
            MobileCount = mobileCount,
            DesktopCount = desktopCount,
            TabletCount = tabletCount
        };

        _cache.Set(cacheKey, result, _cacheExpiration);

        return result;
    }

    public async Task<PeriodComparison> ComparePeriodsAsync(
        DateTime currentStart,
        DateTime currentEnd,
        DateTime previousStart,
        DateTime previousEnd,
        string? shortCode = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"compare:{shortCode ?? "all"}:{currentStart:yyyyMMdd}:{currentEnd:yyyyMMdd}:{previousStart:yyyyMMdd}:{previousEnd:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out PeriodComparison? cached) && cached != null)
        {
            return cached;
        }

        var analyticsQuery = _context.Analytics.AsQueryable();
        var urlQuery = _context.Urls.AsQueryable();

        if (!string.IsNullOrEmpty(shortCode))
        {
            analyticsQuery = analyticsQuery.Where(a => a.ShortCode == shortCode);
        }

        // Current period metrics
        var currentAnalytics = analyticsQuery.Where(a => a.Timestamp >= currentStart && a.Timestamp <= currentEnd);
        var currentClicks = await currentAnalytics.CountAsync(cancellationToken);
        var currentUniqueVisitors = await currentAnalytics
            .Select(a => a.IpAddress).Distinct().CountAsync(cancellationToken);
        var currentNewUrls = await urlQuery
            .Where(u => u.CreatedAt >= currentStart && u.CreatedAt <= currentEnd)
            .CountAsync(cancellationToken);

        // Previous period metrics
        var previousAnalytics = analyticsQuery.Where(a => a.Timestamp >= previousStart && a.Timestamp <= previousEnd);
        var previousClicks = await previousAnalytics.CountAsync(cancellationToken);
        var previousUniqueVisitors = await previousAnalytics
            .Select(a => a.IpAddress).Distinct().CountAsync(cancellationToken);
        var previousNewUrls = await urlQuery
            .Where(u => u.CreatedAt >= previousStart && u.CreatedAt <= previousEnd)
            .CountAsync(cancellationToken);

        // Generate comparison series (daily)
        var currentDays = (currentEnd - currentStart).Days + 1;
        var series = new List<ComparisonPoint>();

        for (int i = 0; i < currentDays; i++)
        {
            var currentDay = currentStart.AddDays(i);
            var previousDay = previousStart.AddDays(i);

            var currentDayClicks = await analyticsQuery
                .Where(a => a.Timestamp.Date == currentDay.Date)
                .CountAsync(cancellationToken);

            var previousDayClicks = await analyticsQuery
                .Where(a => a.Timestamp.Date == previousDay.Date)
                .CountAsync(cancellationToken);

            series.Add(new ComparisonPoint
            {
                CurrentTimestamp = currentDay,
                PreviousTimestamp = previousDay,
                CurrentClicks = currentDayClicks,
                PreviousClicks = previousDayClicks
            });
        }

        var result = new PeriodComparison
        {
            CurrentStart = currentStart,
            CurrentEnd = currentEnd,
            PreviousStart = previousStart,
            PreviousEnd = previousEnd,
            CurrentClicks = currentClicks,
            PreviousClicks = previousClicks,
            CurrentUniqueVisitors = currentUniqueVisitors,
            PreviousUniqueVisitors = previousUniqueVisitors,
            CurrentNewUrls = currentNewUrls,
            PreviousNewUrls = previousNewUrls,
            Series = series
        };

        _cache.Set(cacheKey, result, _cacheExpiration);

        return result;
    }
}
