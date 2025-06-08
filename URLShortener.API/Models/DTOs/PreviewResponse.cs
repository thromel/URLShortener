namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Response model for URL preview information
/// </summary>
public record PreviewResponse
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
    /// When the URL was created
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Total number of times the URL has been accessed
    /// </summary>
    public required long AccessCount { get; init; }

    /// <summary>
    /// The complete short URL
    /// </summary>
    public required string ShortUrl { get; init; }

    /// <summary>
    /// Whether the URL is currently active
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Expiration date if set
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Basic metadata about the original URL (title, description, etc.)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}