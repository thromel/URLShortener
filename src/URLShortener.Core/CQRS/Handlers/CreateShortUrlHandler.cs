using MediatR;
using Microsoft.Extensions.Logging;
using URLShortener.Core.CQRS.Commands;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Services;
using System.Diagnostics;

namespace URLShortener.Core.CQRS.Handlers;

public class CreateShortUrlHandler : IRequestHandler<CreateShortUrlCommand, CreateShortUrlResult>
{
    private readonly IUrlRepository _urlRepository;
    private readonly ICacheService _cacheService;
    private readonly IEventStore _eventStore;
    private readonly IAnalyticsService _analyticsService;
    private readonly IGeoLocationService _geoLocationService;
    private readonly IInputValidationService _inputValidationService;
    private readonly ILogger<CreateShortUrlHandler> _logger;

    public CreateShortUrlHandler(
        IUrlRepository urlRepository,
        ICacheService cacheService,
        IEventStore eventStore,
        IAnalyticsService analyticsService,
        IGeoLocationService geoLocationService,
        IInputValidationService inputValidationService,
        ILogger<CreateShortUrlHandler> logger)
    {
        _urlRepository = urlRepository;
        _cacheService = cacheService;
        _eventStore = eventStore;
        _analyticsService = analyticsService;
        _geoLocationService = geoLocationService;
        _inputValidationService = inputValidationService;
        _logger = logger;
    }

    public async Task<CreateShortUrlResult> Handle(CreateShortUrlCommand request, CancellationToken cancellationToken)
    {
        // TODO: Add proper Activity tracing with ActivitySource

        try
        {
            // Validate URL
            if (!_inputValidationService.IsValidUrl(request.OriginalUrl))
            {
                _logger.LogWarning("Invalid URL provided: {Url}", request.OriginalUrl);
                throw new ArgumentException("Invalid URL format or forbidden URL");
            }

            // Sanitize custom alias if provided
            string? sanitizedAlias = null;
            if (!string.IsNullOrWhiteSpace(request.CustomAlias))
            {
                sanitizedAlias = _inputValidationService.SanitizeCustomAlias(request.CustomAlias);
            }

            // Create aggregate
            var aggregate = ShortUrlAggregate.Create(
                originalUrl: request.OriginalUrl,
                userId: request.UserId,
                customAlias: sanitizedAlias,
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

            // Clear uncommitted events
            aggregate.ClearUncommittedEvents();

            _logger.LogInformation("Successfully created short URL {ShortCode} for user {UserId}", 
                aggregate.ShortCode, request.UserId);

            // TODO: Activity tracing

            return new CreateShortUrlResult
            {
                ShortCode = aggregate.ShortCode,
                ShortUrl = $"https://short.ly/{aggregate.ShortCode}", // This should come from configuration
                OriginalUrl = aggregate.OriginalUrl,
                CreatedAt = aggregate.CreatedAt,
                UserId = aggregate.CreatedBy,
                ExpiresAt = aggregate.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create short URL for {OriginalUrl}", request.OriginalUrl);
            // TODO: Activity tracing
            throw;
        }
    }
}

public class DeleteUrlHandler : IRequestHandler<DeleteUrlCommand, bool>
{
    private readonly IUrlRepository _urlRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DeleteUrlHandler> _logger;

    public DeleteUrlHandler(
        IUrlRepository urlRepository,
        ICacheService cacheService,
        ILogger<DeleteUrlHandler> logger)
    {
        _urlRepository = urlRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteUrlCommand request, CancellationToken cancellationToken)
    {
        // TODO: Add proper Activity tracing

        try
        {
            // Check if the URL exists and belongs to the user (or user has permission)
            var url = await _urlRepository.GetByShortCodeAsync(request.ShortCode);
            if (url == null)
            {
                return false;
            }

            // For now, delete regardless of user (can add authorization logic later)
            var deleted = await _urlRepository.DeleteAsync(request.ShortCode);
            
            if (deleted)
            {
                // Remove from cache
                await _cacheService.InvalidateAsync(request.ShortCode, CacheInvalidationReason.UrlDeleted);
                _logger.LogInformation("Successfully deleted URL {ShortCode} by user {UserId}", 
                    request.ShortCode, request.UserId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete URL {ShortCode} for user {UserId}", 
                request.ShortCode, request.UserId);
            throw;
        }
    }
}