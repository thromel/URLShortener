using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Events.Handlers;

public class UrlCreatedEventHandler : IDomainEventHandler<UrlCreatedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UrlCreatedEventHandler> _logger;

    public UrlCreatedEventHandler(
        IAnalyticsService analyticsService,
        ICacheService cacheService,
        ILogger<UrlCreatedEventHandler> logger)
    {
        _analyticsService = analyticsService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task HandleAsync(UrlCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling URL created event for {ShortCode}", domainEvent.ShortCode);

        try
        {
            // Cache the new URL for faster access
            await _cacheService.SetAsync(domainEvent.ShortCode, domainEvent.OriginalUrl, TimeSpan.FromHours(1));

            // Record creation analytics
            await _analyticsService.RecordCreationAsync(
                domainEvent.ShortCode,
                domainEvent.OriginalUrl,
                domainEvent.UserId.ToString());

            _logger.LogDebug("Successfully processed URL created event for {ShortCode}", domainEvent.ShortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle URL created event for {ShortCode}", domainEvent.ShortCode);
            // Don't rethrow - we don't want to break the main flow
        }
    }
}