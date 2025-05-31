using Microsoft.Extensions.Logging;
using URLShortener.Core.Interfaces;

namespace URLShortener.Infrastructure.Services;

public class CloudFrontCacheService : ICdnCache
{
    private readonly ILogger<CloudFrontCacheService> _logger;

    public CloudFrontCacheService(ILogger<CloudFrontCacheService> logger)
    {
        _logger = logger;
    }

    public async Task InvalidateAsync(string path)
    {
        try
        {
            // In production, this would call AWS CloudFront invalidation API
            // For now, we'll just log the invalidation request
            _logger.LogInformation("CDN invalidation requested for path: {Path}", path);

            // Simulate API call delay
            await Task.Delay(100);

            // Example of what the real implementation would look like:
            /*
            using var client = new AmazonCloudFrontClient();
            var request = new CreateInvalidationRequest
            {
                DistributionId = "YOUR_DISTRIBUTION_ID",
                InvalidationBatch = new InvalidationBatch
                {
                    Paths = new Paths
                    {
                        Quantity = 1,
                        Items = new List<string> { path }
                    },
                    CallerReference = Guid.NewGuid().ToString()
                }
            };
            
            await client.CreateInvalidationAsync(request);
            */
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate CDN cache for path: {Path}", path);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string path)
    {
        try
        {
            // In production, this would check if the path exists in CloudFront cache
            // For now, we'll simulate a cache check
            _logger.LogDebug("Checking CDN cache existence for path: {Path}", path);

            // Simulate API call delay
            await Task.Delay(50);

            // For demo purposes, assume cache exists for paths that don't start with "new-"
            return !path.Contains("new-");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check CDN cache existence for path: {Path}", path);
            return false;
        }
    }
}