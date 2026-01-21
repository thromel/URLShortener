namespace URLShortener.API.Hubs;

/// <summary>
/// Defines the client-side methods that can be invoked from the server via SignalR.
/// Clients implement these methods to receive real-time analytics updates.
/// </summary>
public interface IAnalyticsClient
{
    /// <summary>
    /// Receives a real-time URL access event notification.
    /// Called when a short URL is accessed/clicked.
    /// </summary>
    /// <param name="shortCode">The short code that was accessed.</param>
    /// <param name="data">The access event details.</param>
    Task ReceiveAccessEvent(string shortCode, AccessEventDto data);

    /// <summary>
    /// Receives updated statistics for a URL.
    /// Called periodically or when significant changes occur.
    /// </summary>
    /// <param name="shortCode">The short code for the statistics.</param>
    /// <param name="stats">The updated statistics.</param>
    Task ReceiveStatisticsUpdate(string shortCode, StatisticsUpdateDto stats);

    /// <summary>
    /// Receives a notification when a new URL is created.
    /// Only sent to users subscribed to their own URL feed.
    /// </summary>
    /// <param name="data">The newly created URL details.</param>
    Task ReceiveUrlCreated(UrlCreatedDto data);

    /// <summary>
    /// Receives a notification when a URL is deleted.
    /// </summary>
    /// <param name="shortCode">The short code that was deleted.</param>
    Task ReceiveUrlDeleted(string shortCode);

    /// <summary>
    /// Receives a notification when a URL expires.
    /// </summary>
    /// <param name="shortCode">The short code that expired.</param>
    Task ReceiveUrlExpired(string shortCode);

    /// <summary>
    /// Receives a system-wide notification or alert.
    /// Used for maintenance notices, rate limit warnings, etc.
    /// </summary>
    /// <param name="message">The notification message.</param>
    /// <param name="severity">The severity level (info, warning, error).</param>
    Task ReceiveSystemNotification(string message, string severity);
}

/// <summary>
/// Data transfer object for URL access events.
/// </summary>
public record AccessEventDto(
    string ShortCode,
    DateTime AccessedAt,
    string? Country,
    string? City,
    string DeviceType,
    string Browser,
    string OperatingSystem,
    string? Referrer,
    bool IsMobile
);

/// <summary>
/// Data transfer object for statistics updates.
/// </summary>
public record StatisticsUpdateDto(
    string ShortCode,
    long TotalClicks,
    long TodayClicks,
    long UniqueVisitors,
    Dictionary<string, long> TopCountries,
    Dictionary<string, long> TopDevices,
    Dictionary<string, long> TopBrowsers,
    DateTime LastUpdated
);

/// <summary>
/// Data transfer object for URL creation notifications.
/// </summary>
public record UrlCreatedDto(
    string ShortCode,
    string ShortUrl,
    string OriginalUrl,
    DateTime CreatedAt,
    DateTime? ExpiresAt
);
