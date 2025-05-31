using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using System.Text.RegularExpressions;

namespace URLShortener.Core.Services;

public class UrlService : IUrlService
{
    private readonly IUrlRepository _repository;
    private readonly IEventStore _eventStore;
    private readonly ICacheService _cacheService;
    private readonly IAnalyticsService _analytics;
    private readonly IGeoLocationService _geoLocation;
    private readonly ILogger<UrlService> _logger;

    // URL validation regex
    private static readonly Regex UrlRegex = new(
        @"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Blacklisted domains for security
    private static readonly HashSet<string> BlacklistedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "malware-site.com",
        "phishing-site.com",
        "spam-site.com"
    };

    public UrlService(
        IUrlRepository repository,
        IEventStore eventStore,
        ICacheService cacheService,
        IAnalyticsService analytics,
        IGeoLocationService geoLocation,
        ILogger<UrlService> logger)
    {
        _repository = repository;
        _eventStore = eventStore;
        _cacheService = cacheService;
        _analytics = analytics;
        _geoLocation = geoLocation;
        _logger = logger;
    }

    public async Task<string> CreateShortUrlAsync(CreateUrlRequest request)
    {
        _logger.LogInformation("Creating short URL for {OriginalUrl}", request.OriginalUrl);

        // Validate the URL
        await ValidateUrlAsync(request.OriginalUrl);

        // Check custom alias availability
        if (!string.IsNullOrEmpty(request.CustomAlias))
        {
            if (await _repository.ExistsAsync(request.CustomAlias))
            {
                throw new InvalidOperationException($"Custom alias '{request.CustomAlias}' is already taken");
            }
        }

        // Create the aggregate
        var aggregate = ShortUrlAggregate.Create(
            originalUrl: request.OriginalUrl,
            userId: request.UserId,
            customAlias: request.CustomAlias,
            expiresAt: request.ExpiresAt,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            metadata: request.Metadata
        );

        // Save to event store
        await _eventStore.SaveEventsAsync(aggregate.Id, aggregate.GetUncommittedEvents(), 0);

        // Save to repository for read model
        await _repository.SaveAsync(aggregate);

        // Cache the URL for fast access
        await _cacheService.SetAsync(aggregate.ShortCode, aggregate.OriginalUrl,
            request.ExpiresAt?.Subtract(DateTime.UtcNow));

        _logger.LogInformation("Created short URL {ShortCode} for {OriginalUrl}",
            aggregate.ShortCode, request.OriginalUrl);

        return aggregate.ShortCode;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        // Try cache first for performance
        var cachedUrl = await _cacheService.GetOriginalUrlAsync(shortCode);
        if (!string.IsNullOrEmpty(cachedUrl))
        {
            return cachedUrl;
        }

        // Fallback to repository
        var originalUrl = await _repository.GetOriginalUrlAsync(shortCode);

        // Cache the result if found
        if (!string.IsNullOrEmpty(originalUrl))
        {
            await _cacheService.SetAsync(shortCode, originalUrl);
        }

        return originalUrl;
    }

    public async Task RecordAccessAsync(string shortCode, string ipAddress, string userAgent, string referrer = "")
    {
        try
        {
            // Get the aggregate to record the access
            var aggregate = await _repository.GetByShortCodeAsync(shortCode);
            if (aggregate == null)
            {
                _logger.LogWarning("Attempted to record access for non-existent short code: {ShortCode}", shortCode);
                return;
            }

            // Get geolocation data
            var geoLocation = await _geoLocation.GetLocationAsync(ipAddress);
            var deviceInfo = ParseUserAgent(userAgent);

            // Record the access on the aggregate
            aggregate.RecordAccess(ipAddress, userAgent, referrer, geoLocation, deviceInfo);

            // Save events
            await _eventStore.SaveEventsAsync(aggregate.Id, aggregate.GetUncommittedEvents(), aggregate.Version - 1);

            // Update read model
            await _repository.SaveAsync(aggregate);

            _logger.LogDebug("Recorded access for {ShortCode} from {IpAddress}", shortCode, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record access for {ShortCode}", shortCode);
            // Don't throw - access recording shouldn't break the redirect
        }
    }

    public async Task<UrlStatistics> GetUrlStatisticsAsync(string shortCode)
    {
        var aggregate = await _repository.GetByShortCodeAsync(shortCode);
        if (aggregate == null)
        {
            throw new ArgumentException($"Short code '{shortCode}' not found");
        }

        // Get detailed analytics from analytics service
        var analyticsSummary = await _analytics.GetSummaryAsync(shortCode);

        return new UrlStatistics(
            ShortCode: aggregate.ShortCode,
            OriginalUrl: aggregate.OriginalUrl,
            AccessCount: aggregate.AccessCount,
            CreatedAt: aggregate.CreatedAt,
            LastAccessedAt: aggregate.LastAccessedAt,
            ExpiresAt: aggregate.ExpiresAt,
            Status: aggregate.Status,
            CountryStats: analyticsSummary.CountryBreakdown,
            DeviceStats: analyticsSummary.DeviceBreakdown,
            ReferrerStats: new Dictionary<string, long>() // Could be extracted from events
        );
    }

    public async Task<bool> DeleteUrlAsync(string shortCode)
    {
        var aggregate = await _repository.GetByShortCodeAsync(shortCode);
        if (aggregate == null)
        {
            return false;
        }

        // Disable instead of delete to maintain audit trail
        aggregate.Disable(DisableReason.AdminAction, "Deleted by user");

        // Save events
        await _eventStore.SaveEventsAsync(aggregate.Id, aggregate.GetUncommittedEvents(), aggregate.Version - 1);

        // Update read model
        await _repository.SaveAsync(aggregate);

        // Invalidate cache
        await _cacheService.InvalidateAsync(shortCode, CacheInvalidationReason.UrlDeleted);

        _logger.LogInformation("Deleted URL {ShortCode}", shortCode);

        return true;
    }

    public async Task<IEnumerable<UrlStatistics>> GetUserUrlsAsync(Guid userId, int skip = 0, int take = 50)
    {
        var aggregates = await _repository.GetByUserIdAsync(userId, skip, take);

        var statistics = new List<UrlStatistics>();
        foreach (var aggregate in aggregates)
        {
            var analyticsSummary = await _analytics.GetSummaryAsync(aggregate.ShortCode);

            statistics.Add(new UrlStatistics(
                ShortCode: aggregate.ShortCode,
                OriginalUrl: aggregate.OriginalUrl,
                AccessCount: aggregate.AccessCount,
                CreatedAt: aggregate.CreatedAt,
                LastAccessedAt: aggregate.LastAccessedAt,
                ExpiresAt: aggregate.ExpiresAt,
                Status: aggregate.Status,
                CountryStats: analyticsSummary.CountryBreakdown,
                DeviceStats: analyticsSummary.DeviceBreakdown,
                ReferrerStats: new Dictionary<string, long>()
            ));
        }

        return statistics;
    }

    public async Task<bool> IsAvailableAsync(string shortCode)
    {
        return !await _repository.ExistsAsync(shortCode);
    }

    public async Task DisableUrlAsync(string shortCode, DisableReason reason, string? adminNotes = null)
    {
        var aggregate = await _repository.GetByShortCodeAsync(shortCode);
        if (aggregate == null)
        {
            throw new ArgumentException($"Short code '{shortCode}' not found");
        }

        aggregate.Disable(reason, adminNotes);

        // Save events
        await _eventStore.SaveEventsAsync(aggregate.Id, aggregate.GetUncommittedEvents(), aggregate.Version - 1);

        // Update read model
        await _repository.SaveAsync(aggregate);

        // Invalidate cache
        var invalidationReason = reason switch
        {
            DisableReason.SuspiciousActivity => CacheInvalidationReason.SuspiciousActivity,
            DisableReason.PolicyViolation => CacheInvalidationReason.PolicyViolation,
            _ => CacheInvalidationReason.ManualInvalidation
        };

        await _cacheService.InvalidateAsync(shortCode, invalidationReason);

        _logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, reason);
    }

    public async Task<IEnumerable<UrlStatistics>> SearchUrlsAsync(string searchTerm, int skip = 0, int take = 50)
    {
        var aggregates = await _repository.SearchAsync(searchTerm, skip, take);

        var statistics = new List<UrlStatistics>();
        foreach (var aggregate in aggregates)
        {
            var analyticsSummary = await _analytics.GetSummaryAsync(aggregate.ShortCode);

            statistics.Add(new UrlStatistics(
                ShortCode: aggregate.ShortCode,
                OriginalUrl: aggregate.OriginalUrl,
                AccessCount: aggregate.AccessCount,
                CreatedAt: aggregate.CreatedAt,
                LastAccessedAt: aggregate.LastAccessedAt,
                ExpiresAt: aggregate.ExpiresAt,
                Status: aggregate.Status,
                CountryStats: analyticsSummary.CountryBreakdown,
                DeviceStats: analyticsSummary.DeviceBreakdown,
                ReferrerStats: new Dictionary<string, long>()
            ));
        }

        return statistics;
    }

    private async Task ValidateUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty");
        }

        if (!UrlRegex.IsMatch(url))
        {
            throw new ArgumentException("Invalid URL format");
        }

        // Check against blacklisted domains
        var uri = new Uri(url);
        if (BlacklistedDomains.Contains(uri.Host))
        {
            throw new ArgumentException($"Domain '{uri.Host}' is not allowed");
        }

        // Additional security checks could be added here:
        // - Check against threat intelligence APIs
        // - Validate URL is reachable
        // - Check for redirects to malicious sites
    }

    private static DeviceInfo ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return new DeviceInfo("Unknown", "Unknown", "Unknown", false);
        }

        var isMobile = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                      userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                      userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase);

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

        var deviceType = isMobile ? "Mobile" : "Desktop";

        return new DeviceInfo(deviceType, browser, os, isMobile);
    }
}

public interface IGeoLocationService
{
    Task<GeoLocation> GetLocationAsync(string ipAddress);
}

public class GeoLocationService : IGeoLocationService
{
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(ILogger<GeoLocationService> logger)
    {
        _logger = logger;
    }

    public async Task<GeoLocation> GetLocationAsync(string ipAddress)
    {
        try
        {
            // This is a simplified implementation
            // In production, you would integrate with a service like:
            // - MaxMind GeoIP2
            // - IP2Location
            // - ipapi.co

            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "127.0.0.1" || ipAddress == "::1")
            {
                return new GeoLocation("Unknown", "Unknown", "Unknown", 0, 0);
            }

            // Mock implementation - replace with actual geolocation service
            return new GeoLocation("US", "California", "San Francisco", 37.7749, -122.4194);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get geolocation for IP {IpAddress}", ipAddress);
            return new GeoLocation("Unknown", "Unknown", "Unknown", 0, 0);
        }
    }
}