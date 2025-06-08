using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Events.Handlers;

public class UrlAccessedEventHandler : IDomainEventHandler<UrlAccessedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<UrlAccessedEventHandler> _logger;

    public UrlAccessedEventHandler(
        IAnalyticsService analyticsService,
        ILogger<UrlAccessedEventHandler> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task HandleAsync(UrlAccessedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling URL accessed event for {ShortCode}", domainEvent.ShortCode);

        try
        {
            // Record access analytics asynchronously
            await _analyticsService.RecordAccessAsync(
                domainEvent.ShortCode,
                domainEvent.IpAddress,
                domainEvent.UserAgent,
                domainEvent.Referrer,
                domainEvent.Location,
                domainEvent.DeviceInfo);

            _logger.LogDebug("Successfully processed URL accessed event for {ShortCode}", domainEvent.ShortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle URL accessed event for {ShortCode}", domainEvent.ShortCode);
            // Don't rethrow - we don't want to break the main flow
        }
    }
}

public class UrlDisabledEventHandler : IDomainEventHandler<UrlDisabledEvent>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<UrlDisabledEventHandler> _logger;

    public UrlDisabledEventHandler(
        ICacheService cacheService,
        ILogger<UrlDisabledEventHandler> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(UrlDisabledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling URL disabled event for {ShortCode}, reason: {Reason}", 
            domainEvent.ShortCode, domainEvent.Reason);

        try
        {
            // Remove from cache since it's disabled
            await _cacheService.InvalidateAsync(domainEvent.ShortCode, CacheInvalidationReason.UrlDisabled);

            _logger.LogDebug("Successfully processed URL disabled event for {ShortCode}", domainEvent.ShortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle URL disabled event for {ShortCode}", domainEvent.ShortCode);
            // Don't rethrow - we don't want to break the main flow
        }
    }
}