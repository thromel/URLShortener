namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Response model for URL availability check
/// </summary>
public record AvailabilityResponse
{
    /// <summary>
    /// The short code being checked
    /// </summary>
    public required string ShortCode { get; init; }

    /// <summary>
    /// Whether the short code is available for use
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Reason why the short code is not available (if applicable)
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Alternative suggestions if the short code is not available
    /// </summary>
    public List<string>? Suggestions { get; init; }
}