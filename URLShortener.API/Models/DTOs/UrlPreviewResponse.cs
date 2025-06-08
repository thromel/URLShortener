namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Response model for URL preview with OpenGraph metadata
/// </summary>
public record UrlPreviewResponse
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
    /// Page title from OpenGraph or HTML title
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Page description from OpenGraph or meta description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Preview image URL from OpenGraph
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Site name from OpenGraph
    /// </summary>
    public string? SiteName { get; init; }

    /// <summary>
    /// Favicon URL
    /// </summary>
    public string? FaviconUrl { get; init; }

    /// <summary>
    /// Page type (website, article, video, etc.)
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// When the preview was fetched
    /// </summary>
    public DateTime FetchedAt { get; init; } = DateTime.UtcNow;
}