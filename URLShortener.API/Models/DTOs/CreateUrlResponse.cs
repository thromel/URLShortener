namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Response model for successful URL creation
/// </summary>
public record CreateUrlResponse
{
    /// <summary>
    /// The generated short code
    /// </summary>
    public required string ShortCode { get; init; }

    /// <summary>
    /// The complete short URL
    /// </summary>
    public required string ShortUrl { get; init; }

    /// <summary>
    /// The original URL that was shortened
    /// </summary>
    public required string OriginalUrl { get; init; }

    /// <summary>
    /// Timestamp when the URL was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who created the URL (if authenticated)
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Expiration date if set
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Error message if creation failed (used in bulk operations)
    /// </summary>
    public string? Error { get; init; }
}