using MediatR;
using Microsoft.Extensions.Logging;
using URLShortener.Core.CQRS.Queries;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Telemetry;
using System.Diagnostics;

namespace URLShortener.Core.CQRS.Handlers;

public class GetUrlQueryHandler : IRequestHandler<GetUrlQuery, UrlStatistics?>
{
    private readonly IUrlRepository _urlRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<GetUrlQueryHandler> _logger;

    public GetUrlQueryHandler(
        IUrlRepository urlRepository,
        IAnalyticsService analyticsService,
        ILogger<GetUrlQueryHandler> logger)
    {
        _urlRepository = urlRepository;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<UrlStatistics?> Handle(GetUrlQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.GetUrl, ActivityKind.Internal);
        activity?.SetTag(Diagnostics.Tags.ShortCode, request.ShortCode);

        try
        {
            // Get URL from repository
            var url = await _urlRepository.GetByShortCodeAsync(request.ShortCode);
            if (url == null)
            {
                activity?.SetTag("url.found", false);
                Diagnostics.RecordSuccess(activity);
                return null;
            }

            activity?.SetTag("url.found", true);
            activity?.SetTag(Diagnostics.Tags.UrlStatus, url.Status.ToString());

            // Get analytics summary
            var summary = await _analyticsService.GetSummaryAsync(request.ShortCode);

            Diagnostics.RecordSuccess(activity);
            return new UrlStatistics(
                ShortCode: url.ShortCode,
                OriginalUrl: url.OriginalUrl,
                AccessCount: url.AccessCount,
                CreatedAt: url.CreatedAt,
                LastAccessedAt: url.LastAccessedAt,
                ExpiresAt: url.ExpiresAt,
                Status: url.Status,
                CountryStats: summary.CountryBreakdown,
                DeviceStats: summary.DeviceBreakdown,
                ReferrerStats: new Dictionary<string, long>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URL statistics for {ShortCode}", request.ShortCode);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }
}

public class GetOriginalUrlQueryHandler : IRequestHandler<GetOriginalUrlQuery, string?>
{
    private readonly ICacheService _cacheService;
    private readonly IUrlRepository _urlRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly ILogger<GetOriginalUrlQueryHandler> _logger;

    public GetOriginalUrlQueryHandler(
        ICacheService cacheService,
        IUrlRepository urlRepository,
        IAnalyticsService analyticsService,
        IGeoLocationService geoLocationService,
        ILogger<GetOriginalUrlQueryHandler> logger)
    {
        _cacheService = cacheService;
        _urlRepository = urlRepository;
        _analyticsService = analyticsService;
        _geoLocationService = geoLocationService;
        _logger = logger;
    }

    public async Task<string?> Handle(GetOriginalUrlQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.GetOriginalUrl, ActivityKind.Internal);
        activity?.SetTag(Diagnostics.Tags.ShortCode, request.ShortCode);

        try
        {
            // Try cache first
            var cachedUrl = await _cacheService.GetOriginalUrlAsync(request.ShortCode);
            if (!string.IsNullOrEmpty(cachedUrl))
            {
                activity?.SetTag(Diagnostics.Tags.CacheHit, true);
                activity?.SetTag(Diagnostics.Tags.CacheLevel, "L1/L2");

                // Record access asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var location = await _geoLocationService.GetLocationAsync(request.IpAddress);
                        var deviceInfo = ParseDeviceInfo(request.UserAgent);
                        await _analyticsService.RecordAccessAsync(request.ShortCode, request.IpAddress,
                            request.UserAgent, request.Referrer, location, deviceInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record analytics for {ShortCode}", request.ShortCode);
                    }
                }, cancellationToken);

                Diagnostics.RecordSuccess(activity);
                return cachedUrl;
            }

            activity?.SetTag(Diagnostics.Tags.CacheHit, false);

            // Fallback to repository
            var originalUrl = await _urlRepository.GetOriginalUrlAsync(request.ShortCode);

            if (!string.IsNullOrEmpty(originalUrl))
            {
                activity?.SetTag("url.found", true);

                // Cache for future requests
                await _cacheService.SetAsync(request.ShortCode, originalUrl);

                // Record access
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var location = await _geoLocationService.GetLocationAsync(request.IpAddress);
                        var deviceInfo = ParseDeviceInfo(request.UserAgent);
                        await _analyticsService.RecordAccessAsync(request.ShortCode, request.IpAddress,
                            request.UserAgent, request.Referrer, location, deviceInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record analytics for {ShortCode}", request.ShortCode);
                    }
                }, cancellationToken);
            }
            else
            {
                activity?.SetTag("url.found", false);
            }

            Diagnostics.RecordSuccess(activity);
            return originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get original URL for {ShortCode}", request.ShortCode);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }

    private static DeviceInfo ParseDeviceInfo(string userAgent)
    {
        var isMobile = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase);
        var deviceType = isMobile ? "Mobile" : "Desktop";

        var browser = "Unknown";
        if (userAgent.Contains("Chrome")) browser = "Chrome";
        else if (userAgent.Contains("Firefox")) browser = "Firefox";
        else if (userAgent.Contains("Safari")) browser = "Safari";
        else if (userAgent.Contains("Edge")) browser = "Edge";

        var os = "Unknown";
        if (userAgent.Contains("Windows")) os = "Windows";
        else if (userAgent.Contains("Mac")) os = "macOS";
        else if (userAgent.Contains("Linux")) os = "Linux";
        else if (userAgent.Contains("Android")) os = "Android";
        else if (userAgent.Contains("iOS")) os = "iOS";

        return new DeviceInfo(deviceType, browser, os, isMobile);
    }
}

public class GetUrlStatisticsQueryHandler : IRequestHandler<GetUrlStatisticsQuery, UrlStatistics?>
{
    private readonly IUrlRepository _urlRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<GetUrlStatisticsQueryHandler> _logger;

    public GetUrlStatisticsQueryHandler(
        IUrlRepository urlRepository,
        IAnalyticsService analyticsService,
        ILogger<GetUrlStatisticsQueryHandler> logger)
    {
        _urlRepository = urlRepository;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<UrlStatistics?> Handle(GetUrlStatisticsQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.GetUrlStatistics, ActivityKind.Internal);
        activity?.SetTag(Diagnostics.Tags.ShortCode, request.ShortCode);

        try
        {
            // Check if URL exists
            var url = await _urlRepository.GetByShortCodeAsync(request.ShortCode);
            if (url == null)
            {
                activity?.SetTag("url.found", false);
                Diagnostics.RecordSuccess(activity);
                return null;
            }

            activity?.SetTag("url.found", true);

            // Get analytics summary
            var summary = await _analyticsService.GetSummaryAsync(request.ShortCode);

            Diagnostics.RecordSuccess(activity);
            return new UrlStatistics(
                ShortCode: url.ShortCode,
                OriginalUrl: url.OriginalUrl,
                AccessCount: url.AccessCount,
                CreatedAt: url.CreatedAt,
                LastAccessedAt: url.LastAccessedAt,
                ExpiresAt: url.ExpiresAt,
                Status: url.Status,
                CountryStats: summary.CountryBreakdown,
                DeviceStats: summary.DeviceBreakdown,
                ReferrerStats: new Dictionary<string, long>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics for {ShortCode}", request.ShortCode);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }
}

public class CheckUrlAvailabilityQueryHandler : IRequestHandler<CheckUrlAvailabilityQuery, bool>
{
    private readonly ICacheService _cacheService;
    private readonly IUrlRepository _urlRepository;
    private readonly ILogger<CheckUrlAvailabilityQueryHandler> _logger;

    public CheckUrlAvailabilityQueryHandler(
        ICacheService cacheService,
        IUrlRepository urlRepository,
        ILogger<CheckUrlAvailabilityQueryHandler> logger)
    {
        _cacheService = cacheService;
        _urlRepository = urlRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(CheckUrlAvailabilityQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.CheckUrlAvailability, ActivityKind.Internal);
        activity?.SetTag(Diagnostics.Tags.ShortCode, request.ShortCode);

        try
        {
            // Check cache first
            if (await _cacheService.ExistsAsync(request.ShortCode))
            {
                activity?.SetTag(Diagnostics.Tags.CacheHit, true);
                activity?.SetTag("url.available", false);
                Diagnostics.RecordSuccess(activity);
                return false;
            }

            activity?.SetTag(Diagnostics.Tags.CacheHit, false);

            // Check repository
            var isAvailable = !await _urlRepository.ExistsAsync(request.ShortCode);

            activity?.SetTag("url.available", isAvailable);
            Diagnostics.RecordSuccess(activity);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check availability for {ShortCode}", request.ShortCode);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }
}

public class GetUserUrlsQueryHandler : IRequestHandler<GetUserUrlsQuery, IEnumerable<UrlStatistics>>
{
    private readonly IUrlRepository _urlRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<GetUserUrlsQueryHandler> _logger;

    public GetUserUrlsQueryHandler(
        IUrlRepository urlRepository,
        IAnalyticsService analyticsService,
        ILogger<GetUserUrlsQueryHandler> logger)
    {
        _urlRepository = urlRepository;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<IEnumerable<UrlStatistics>> Handle(GetUserUrlsQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.GetUserUrls, ActivityKind.Internal);
        activity?.SetTag(Diagnostics.Tags.UserId, request.UserId);
        activity?.SetTag("query.skip", request.Skip);
        activity?.SetTag("query.take", request.Take);

        try
        {
            var urls = await _urlRepository.GetByUserIdAsync(request.UserId, request.Skip, request.Take);
            var urlStatistics = new List<UrlStatistics>();

            foreach (var url in urls)
            {
                var summary = await _analyticsService.GetSummaryAsync(url.ShortCode);
                urlStatistics.Add(new UrlStatistics(
                    ShortCode: url.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    AccessCount: url.AccessCount,
                    CreatedAt: url.CreatedAt,
                    LastAccessedAt: url.LastAccessedAt,
                    ExpiresAt: url.ExpiresAt,
                    Status: url.Status,
                    CountryStats: summary.CountryBreakdown,
                    DeviceStats: summary.DeviceBreakdown,
                    ReferrerStats: new Dictionary<string, long>()
                ));
            }

            activity?.SetTag(Diagnostics.Tags.ResultCount, urlStatistics.Count);
            Diagnostics.RecordSuccess(activity);
            return urlStatistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URLs for user {UserId}", request.UserId);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }
}

public class SearchUrlsQueryHandler : IRequestHandler<SearchUrlsQuery, IEnumerable<UrlStatistics>>
{
    private readonly IUrlRepository _urlRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<SearchUrlsQueryHandler> _logger;

    public SearchUrlsQueryHandler(
        IUrlRepository urlRepository,
        IAnalyticsService analyticsService,
        ILogger<SearchUrlsQueryHandler> logger)
    {
        _urlRepository = urlRepository;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<IEnumerable<UrlStatistics>> Handle(SearchUrlsQuery request, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.StartActivity(Diagnostics.Operations.SearchUrls, ActivityKind.Internal);
        activity?.SetTag("query.search_term", request.SearchTerm);
        activity?.SetTag("query.skip", request.Skip);
        activity?.SetTag("query.take", request.Take);

        try
        {
            var urls = await _urlRepository.SearchAsync(request.SearchTerm, request.Skip, request.Take);
            var urlStatistics = new List<UrlStatistics>();

            foreach (var url in urls)
            {
                var summary = await _analyticsService.GetSummaryAsync(url.ShortCode);
                urlStatistics.Add(new UrlStatistics(
                    ShortCode: url.ShortCode,
                    OriginalUrl: url.OriginalUrl,
                    AccessCount: url.AccessCount,
                    CreatedAt: url.CreatedAt,
                    LastAccessedAt: url.LastAccessedAt,
                    ExpiresAt: url.ExpiresAt,
                    Status: url.Status,
                    CountryStats: summary.CountryBreakdown,
                    DeviceStats: summary.DeviceBreakdown,
                    ReferrerStats: new Dictionary<string, long>()
                ));
            }

            activity?.SetTag(Diagnostics.Tags.ResultCount, urlStatistics.Count);
            Diagnostics.RecordSuccess(activity);
            return urlStatistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search URLs with term {SearchTerm}", request.SearchTerm);
            Diagnostics.RecordException(activity, ex);
            throw;
        }
    }
}