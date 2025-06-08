using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Request model for creating a new short URL
/// </summary>
public record CreateUrlDto
{
    /// <summary>
    /// The original URL to be shortened
    /// </summary>
    [Required]
    [Url]
    [StringLength(2048, MinimumLength = 10)]
    public required string OriginalUrl { get; init; }

    /// <summary>
    /// Optional custom alias for the short URL (3-50 characters, alphanumeric and hyphens only)
    /// </summary>
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9-]+$", ErrorMessage = "Custom alias can only contain letters, numbers, and hyphens")]
    public string? CustomAlias { get; init; }

    /// <summary>
    /// Optional expiration date for the short URL
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Optional metadata associated with the URL
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}