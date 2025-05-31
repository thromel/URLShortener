using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Services;

public class PredictiveCacheWarmingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PredictiveCacheWarmingService> _logger;

    public PredictiveCacheWarmingService(
        IServiceProvider serviceProvider,
        ILogger<PredictiveCacheWarmingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Predictive cache warming service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WarmCacheAsync();
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in predictive cache warming service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Predictive cache warming service stopped");
    }

    private async Task WarmCacheAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var urlRepository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

        try
        {
            // Get trending URLs from the last hour
            var trendingUrls = await analyticsService.GetTrendingUrlsAsync(50, TimeSpan.FromHours(1));

            foreach (var url in trendingUrls)
            {
                try
                {
                    // Check if URL is already cached
                    var cachedUrl = await cacheService.GetOriginalUrlAsync(url.ShortCode);
                    if (string.IsNullOrEmpty(cachedUrl))
                    {
                        // Get original URL from repository and cache it
                        var originalUrl = await urlRepository.GetOriginalUrlAsync(url.ShortCode);
                        if (!string.IsNullOrEmpty(originalUrl))
                        {
                            await cacheService.SetAsync(url.ShortCode, originalUrl, TimeSpan.FromHours(2));
                            _logger.LogDebug("Pre-cached trending URL {ShortCode}", url.ShortCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to warm cache for URL {ShortCode}", url.ShortCode);
                }
            }

            _logger.LogInformation("Cache warming completed for {Count} URLs", trendingUrls.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform cache warming");
        }
    }
}