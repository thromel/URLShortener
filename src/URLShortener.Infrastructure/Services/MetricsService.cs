using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Services;

namespace URLShortener.Infrastructure.Services;

public interface IMetrics
{
    void IncrementCounter(string name, Dictionary<string, string>? tags = null);
    void RecordValue(string name, double value, Dictionary<string, string>? tags = null);
    void RecordDuration(string name, TimeSpan duration, Dictionary<string, string>? tags = null);
    void RecordUrlCreated(string userId, bool isCustom);
    void RecordUrlAccessed(string shortCode, double responseTime);
    void RecordCacheHit(string cacheLayer);
    void RecordCacheMiss();
    void RecordError(string errorType);
    void RecordSecurityEvent(string eventType);
    void RecordApiCall(string endpoint, string method, int statusCode, double duration);
}

public class MetricsService : IMetrics
{
    private readonly ILogger<MetricsService> _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _urlsCreated;
    private readonly Counter<long> _urlsAccessed;
    private readonly Histogram<double> _responseTime;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _securityEvents;
    private readonly Histogram<double> _apiCallDuration;

    public MetricsService(ILogger<MetricsService> logger)
    {
        _logger = logger;
        _meter = new Meter("URLShortener.Metrics");

        _urlsCreated = _meter.CreateCounter<long>(
            "urlshortener.urls.created",
            unit: "urls",
            description: "Number of URLs created");

        _urlsAccessed = _meter.CreateCounter<long>(
            "urlshortener.urls.accessed",
            unit: "urls",
            description: "Number of URLs accessed");

        _responseTime = _meter.CreateHistogram<double>(
            "urlshortener.response.time",
            unit: "ms",
            description: "Response time for URL operations");

        _cacheHits = _meter.CreateCounter<long>(
            "urlshortener.cache.hits",
            unit: "hits",
            description: "Number of cache hits");

        _cacheMisses = _meter.CreateCounter<long>(
            "urlshortener.cache.misses",
            unit: "misses",
            description: "Number of cache misses");

        _errors = _meter.CreateCounter<long>(
            "urlshortener.errors",
            unit: "errors",
            description: "Number of errors");

        _securityEvents = _meter.CreateCounter<long>(
            "urlshortener.security.events",
            unit: "events",
            description: "Number of security events");

        _apiCallDuration = _meter.CreateHistogram<double>(
            "urlshortener.api.duration",
            unit: "ms",
            description: "API call duration");
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

    public void RecordUrlCreated(string userId, bool isCustom)
    {
        var tags = new TagList
        {
            { "user_id", userId },
            { "is_custom", isCustom.ToString() }
        };
        _urlsCreated.Add(1, tags);
    }

    public void RecordUrlAccessed(string shortCode, double responseTime)
    {
        var tags = new TagList
        {
            { "short_code", shortCode }
        };
        _urlsAccessed.Add(1, tags);
        _responseTime.Record(responseTime, tags);
    }

    public void RecordCacheHit(string cacheLayer)
    {
        var tags = new TagList
        {
            { "layer", cacheLayer }
        };
        _cacheHits.Add(1, tags);
    }

    public void RecordCacheMiss()
    {
        _cacheMisses.Add(1);
    }

    public void RecordError(string errorType)
    {
        var tags = new TagList
        {
            { "type", errorType }
        };
        _errors.Add(1, tags);
    }

    public void RecordSecurityEvent(string eventType)
    {
        var tags = new TagList
        {
            { "event_type", eventType }
        };
        _securityEvents.Add(1, tags);
        _logger.LogWarning("Security event recorded: {EventType}", eventType);
    }

    public void RecordApiCall(string endpoint, string method, int statusCode, double duration)
    {
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "method", method },
            { "status_code", statusCode.ToString() },
            { "status_class", $"{statusCode / 100}xx" }
        };
        _apiCallDuration.Record(duration, tags);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}