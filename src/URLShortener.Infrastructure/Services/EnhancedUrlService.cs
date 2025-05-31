using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Infrastructure.Services;

namespace URLShortener.Infrastructure.Services;

public class EnhancedUrlService : IUrlService
{
    private readonly IUrlRepository _urlRepository;
    private readonly ICacheService _cacheService;
    private readonly IEventStore _eventStore;
    private readonly IAnalyticsService _analyticsService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly ILogger<EnhancedUrlService> _logger;

    public EnhancedUrlService(
        IUrlRepository urlRepository,
        ICacheService cacheService,
        IEventStore eventStore,
        IAnalyticsService analyticsService,
        IGeoLocationService geoLocationService,
        ILogger<EnhancedUrlService> logger)
    {
        _urlRepository = urlRepository;
        _cacheService = cacheService;
        _eventStore = eventStore;
        _analyticsService = analyticsService;
        _geoLocationService = geoLocationService;
        _logger = logger;
    }

    public async Task<string> CreateShortUrlAsync(CreateUrlRequest request)
    {
        try
        {
            // Validate URL
            if (!Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL format", nameof(request.OriginalUrl));
            }

            // Check for malicious URLs
            await ValidateUrlSafetyAsync(request.OriginalUrl);

            // Create aggregate
            var aggregate = ShortUrlAggregate.Create(
                originalUrl: request.OriginalUrl,
                userId: request.UserId,
                customAlias: request.CustomAlias,
                expiresAt: request.ExpiresAt,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                metadata: request.Metadata
            );

            // Save events
            await _eventStore.SaveEventsAsync(aggregate.Id, aggregate.GetUncommittedEvents(), 0);

            // Update read model
            await _urlRepository.SaveAsync(aggregate);

            // Cache the URL
            await _cacheService.SetAsync(aggregate.ShortCode, aggregate.OriginalUrl);

            aggregate.ClearUncommittedEvents();

            _logger.LogInformation("Created short URL {ShortCode} for user {UserId}",
                aggregate.ShortCode, request.UserId);

            return aggregate.ShortCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create short URL for {OriginalUrl}", request.OriginalUrl);
            throw;
        }
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        try
        {
            // Try cache first
            var cachedUrl = await _cacheService.GetOriginalUrlAsync(shortCode);
            if (!string.IsNullOrEmpty(cachedUrl))
            {
                return cachedUrl;
            }

            // Fallback to repository
            var originalUrl = await _urlRepository.GetOriginalUrlAsync(shortCode);

            // Cache for future requests
            if (!string.IsNullOrEmpty(originalUrl))
            {
                await _cacheService.SetAsync(shortCode, originalUrl);
            }

            return originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get original URL for {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task<UrlStatistics> GetUrlStatisticsAsync(string shortCode)
    {
        try
        {
            var aggregate = await ReconstructAggregateAsync(shortCode);
            if (aggregate == null)
            {
                throw new ArgumentException($"Short code '{shortCode}' not found");
            }

            // Get analytics summary
            var summary = await _analyticsService.GetSummaryAsync(shortCode);

            return new UrlStatistics(
                ShortCode: aggregate.ShortCode,
                OriginalUrl: aggregate.OriginalUrl,
                AccessCount: aggregate.AccessCount,
                CreatedAt: aggregate.CreatedAt,
                LastAccessedAt: aggregate.LastAccessedAt,
                ExpiresAt: aggregate.ExpiresAt,
                Status: aggregate.Status,
                CountryStats: summary.CountryBreakdown,
                DeviceStats: summary.DeviceBreakdown,
                ReferrerStats: new Dictionary<string, long>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics for {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task<bool> DeleteUrlAsync(string shortCode)
    {
        try
        {
            var success = await _urlRepository.DeleteAsync(shortCode);
            if (success)
            {
                await _cacheService.InvalidateAsync(shortCode, CacheInvalidationReason.UrlDeleted);
                _logger.LogInformation("Deleted URL {ShortCode}", shortCode);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete URL {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task<IEnumerable<UrlStatistics>> GetUserUrlsAsync(Guid userId, int skip = 0, int take = 50)
    {
        try
        {
            return await _urlRepository.GetUserUrlsAsync(userId, skip, take);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URLs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(string shortCode)
    {
        try
        {
            // Check cache first
            if (await _cacheService.ExistsAsync(shortCode))
            {
                return false;
            }

            // Check repository
            return !await _urlRepository.ExistsAsync(shortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check availability for {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task RecordAccessAsync(string shortCode, string ipAddress, string userAgent, string referrer = "")
    {
        try
        {
            // Get geolocation data
            var location = await _geoLocationService.GetLocationAsync(ipAddress);
            var deviceInfo = ParseDeviceInfo(userAgent);

            // Reconstruct aggregate to record access
            var aggregate = await ReconstructAggregateAsync(shortCode);
            if (aggregate != null)
            {
                aggregate.RecordAccess(ipAddress, userAgent, referrer, location, deviceInfo);

                // Save new events
                var events = aggregate.GetUncommittedEvents();
                if (events.Any())
                {
                    await _eventStore.SaveEventsAsync(aggregate.Id, events, aggregate.Version - events.Count);
                    await _urlRepository.UpdateAccessCountAsync(shortCode, aggregate.AccessCount);
                    aggregate.ClearUncommittedEvents();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record access for {ShortCode}", shortCode);
            // Don't throw here as this shouldn't break the redirect
        }
    }

    public async Task DisableUrlAsync(string shortCode, DisableReason reason, string? adminNotes = null)
    {
        try
        {
            var aggregate = await ReconstructAggregateAsync(shortCode);
            if (aggregate == null)
            {
                throw new ArgumentException($"Short code '{shortCode}' not found");
            }

            aggregate.Disable(reason, adminNotes);

            // Save events
            var events = aggregate.GetUncommittedEvents();
            if (events.Any())
            {
                await _eventStore.SaveEventsAsync(aggregate.Id, events, aggregate.Version - events.Count);
                await _urlRepository.UpdateStatusAsync(shortCode, aggregate.Status);
                await _cacheService.InvalidateAsync(shortCode, CacheInvalidationReason.PolicyViolation);
                aggregate.ClearUncommittedEvents();
            }

            _logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable URL {ShortCode}", shortCode);
            throw;
        }
    }

    public async Task<IEnumerable<UrlStatistics>> SearchUrlsAsync(string searchTerm, int skip = 0, int take = 50)
    {
        try
        {
            return await _urlRepository.SearchUrlsAsync(searchTerm, skip, take);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search URLs with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    private async Task<ShortUrlAggregate?> ReconstructAggregateAsync(string shortCode)
    {
        try
        {
            var aggregateId = await _urlRepository.GetAggregateIdAsync(shortCode);
            if (aggregateId == null)
            {
                return null;
            }

            var events = await _eventStore.GetEventsAsync(aggregateId.Value);
            return events.Any() ? ShortUrlAggregate.FromEvents(events) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconstruct aggregate for {ShortCode}", shortCode);
            return null;
        }
    }

    private async Task ValidateUrlSafetyAsync(string url)
    {
        // Basic URL safety validation
        // In production, you might integrate with services like Google Safe Browsing API
        var uri = new Uri(url);

        // Block localhost and private IPs in production
        if (uri.IsLoopback || IsPrivateIP(uri.Host))
        {
            await _analyticsService.RecordSecurityEventAsync("", "BLOCKED_PRIVATE_URL");
            throw new ArgumentException("URLs pointing to private networks are not allowed");
        }

        // Add more safety checks as needed
    }

    private static bool IsPrivateIP(string host)
    {
        // Simple check for private IP ranges
        return host.StartsWith("10.") ||
               host.StartsWith("192.168.") ||
               host.StartsWith("172.16.") ||
               host.StartsWith("127.");
    }

    private static DeviceInfo ParseDeviceInfo(string userAgent)
    {
        // Simple user agent parsing
        // In production, use a proper user agent parsing library
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