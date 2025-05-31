using Microsoft.Extensions.Logging;
using URLShortener.Core.Services;

namespace URLShortener.Infrastructure.Services;

public interface IMetrics
{
    void IncrementCounter(string name, Dictionary<string, string>? tags = null);
    void RecordValue(string name, double value, Dictionary<string, string>? tags = null);
    void RecordDuration(string name, TimeSpan duration, Dictionary<string, string>? tags = null);
}

public class MetricsService : IMetrics
{
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
    }

    public void IncrementCounter(string name, Dictionary<string, string>? tags = null)
    {
        // In production, this would send metrics to Prometheus, DataDog, etc.
        var tagsString = tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "";
        _logger.LogDebug("Counter {MetricName} incremented. Tags: {Tags}", name, tagsString);
    }

    public void RecordValue(string name, double value, Dictionary<string, string>? tags = null)
    {
        var tagsString = tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "";
        _logger.LogDebug("Gauge {MetricName} set to {Value}. Tags: {Tags}", name, value, tagsString);
    }

    public void RecordDuration(string name, TimeSpan duration, Dictionary<string, string>? tags = null)
    {
        var tagsString = tags != null ? string.Join(", ", tags.Select(kv => $"{kv.Key}={kv.Value}")) : "";
        _logger.LogDebug("Duration {MetricName} recorded: {Duration}ms. Tags: {Tags}",
            name, duration.TotalMilliseconds, tagsString);
    }
}