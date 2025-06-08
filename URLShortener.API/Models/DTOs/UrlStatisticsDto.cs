namespace URLShortener.API.Models.DTOs;

/// <summary>
/// DTO for URL statistics
/// </summary>
public record UrlStatisticsDto
{
    /// <summary>
    /// The short code
    /// </summary>
    public required string ShortCode { get; init; }

    /// <summary>
    /// The original URL
    /// </summary>
    public required string OriginalUrl { get; init; }

    /// <summary>
    /// Total number of accesses
    /// </summary>
    public long AccessCount { get; init; }

    /// <summary>
    /// When the URL was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Last time the URL was accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; init; }

    /// <summary>
    /// Expiration date if set
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Current status of the URL
    /// </summary>
    public string Status { get; init; } = "Active";

    /// <summary>
    /// Access statistics by country
    /// </summary>
    public Dictionary<string, long> CountryStats { get; init; } = new();

    /// <summary>
    /// Access statistics by device type
    /// </summary>
    public Dictionary<string, long> DeviceStats { get; init; } = new();

    /// <summary>
    /// Access statistics by referrer
    /// </summary>
    public Dictionary<string, long> ReferrerStats { get; init; } = new();

    /// <summary>
    /// Number of unique visitors
    /// </summary>
    public long UniqueVisitors { get; init; }

    /// <summary>
    /// Peak access hour (0-23)
    /// </summary>
    public int? PeakHour { get; init; }

    /// <summary>
    /// Peak access day of week (0=Sunday)
    /// </summary>
    public int? PeakDayOfWeek { get; init; }
}