using Microsoft.FeatureManagement;

namespace URLShortener.API.Features;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureName);
    Task<bool> IsEnabledAsync<TContext>(string featureName, TContext context);
}

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(IFeatureManager featureManager, ILogger<FeatureFlagService> logger)
    {
        _featureManager = featureManager;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string featureName)
    {
        try
        {
            var isEnabled = await _featureManager.IsEnabledAsync(featureName);
            _logger.LogDebug("Feature {FeatureName} is {Status}", featureName, isEnabled ? "enabled" : "disabled");
            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature flag {FeatureName}", featureName);
            return false; // Default to disabled on error
        }
    }

    public async Task<bool> IsEnabledAsync<TContext>(string featureName, TContext context)
    {
        try
        {
            var isEnabled = await _featureManager.IsEnabledAsync(featureName, context);
            _logger.LogDebug("Feature {FeatureName} is {Status} for context {Context}", 
                featureName, isEnabled ? "enabled" : "disabled", typeof(TContext).Name);
            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature flag {FeatureName} with context", featureName);
            return false; // Default to disabled on error
        }
    }
}