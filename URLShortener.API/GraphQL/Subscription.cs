using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using URLShortener.API.GraphQL.Types;

namespace URLShortener.API.GraphQL;

/// <summary>
/// GraphQL Subscription root type for real-time URL analytics.
/// </summary>
public class Subscription
{
    /// <summary>
    /// Subscribes to access events for a specific URL.
    /// </summary>
    /// <param name="shortCode">The short code to subscribe to.</param>
    /// <param name="accessEvent">The access event (provided by resolver).</param>
    /// <returns>Stream of access events.</returns>
    [Subscribe]
    [Topic("{shortCode}")]
    public UrlAccessEventType OnUrlAccessed(
        string shortCode,
        [EventMessage] UrlAccessEventType accessEvent)
    {
        return accessEvent;
    }

    /// <summary>
    /// Subscribes to all access events for URLs owned by the authenticated user.
    /// </summary>
    /// <param name="accessEvent">The access event.</param>
    /// <returns>Stream of access events for user's URLs.</returns>
    [Authorize]
    [Subscribe]
    [Topic("user_{userId}")]
    public UrlAccessEventType OnMyUrlAccessed(
        [EventMessage] UrlAccessEventType accessEvent)
    {
        return accessEvent;
    }

    /// <summary>
    /// Subscribes to statistics updates for a specific URL.
    /// </summary>
    /// <param name="shortCode">The short code to subscribe to.</param>
    /// <param name="statsUpdate">The statistics update.</param>
    /// <returns>Stream of statistics updates.</returns>
    [Subscribe]
    [Topic("stats_{shortCode}")]
    public UrlStatsUpdateType OnUrlStatsUpdated(
        string shortCode,
        [EventMessage] UrlStatsUpdateType statsUpdate)
    {
        return statsUpdate;
    }

    /// <summary>
    /// Subscribes to trending URL updates.
    /// </summary>
    /// <param name="trendingUpdate">The trending URLs update.</param>
    /// <returns>Stream of trending URL updates.</returns>
    [Subscribe]
    [Topic("trending")]
    public TrendingUpdateType OnTrendingUpdated(
        [EventMessage] TrendingUpdateType trendingUpdate)
    {
        return trendingUpdate;
    }
}

/// <summary>
/// Event type for URL access notifications.
/// </summary>
public class UrlAccessEventType
{
    public string ShortCode { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string? Referrer { get; set; }
    public bool IsMobile { get; set; }
}

/// <summary>
/// Event type for statistics updates.
/// </summary>
public class UrlStatsUpdateType
{
    public string ShortCode { get; set; } = string.Empty;
    public long TotalClicks { get; set; }
    public long TodayClicks { get; set; }
    public long UniqueVisitors { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Event type for trending URL updates.
/// </summary>
public class TrendingUpdateType
{
    public DateTime UpdatedAt { get; set; }
    public List<TrendingShortUrlType> Urls { get; set; } = new();
}

/// <summary>
/// Extension methods for publishing GraphQL subscription events.
/// </summary>
public static class SubscriptionExtensions
{
    /// <summary>
    /// Publishes a URL access event to subscribers.
    /// </summary>
    public static async Task PublishUrlAccessedAsync(
        this ITopicEventSender eventSender,
        string shortCode,
        UrlAccessEventType accessEvent)
    {
        await eventSender.SendAsync(shortCode, accessEvent);
    }

    /// <summary>
    /// Publishes a statistics update to subscribers.
    /// </summary>
    public static async Task PublishStatsUpdateAsync(
        this ITopicEventSender eventSender,
        string shortCode,
        UrlStatsUpdateType statsUpdate)
    {
        await eventSender.SendAsync($"stats_{shortCode}", statsUpdate);
    }

    /// <summary>
    /// Publishes trending update to subscribers.
    /// </summary>
    public static async Task PublishTrendingUpdateAsync(
        this ITopicEventSender eventSender,
        TrendingUpdateType trendingUpdate)
    {
        await eventSender.SendAsync("trending", trendingUpdate);
    }
}
