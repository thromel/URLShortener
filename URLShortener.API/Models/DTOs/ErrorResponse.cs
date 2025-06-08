namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Standard error response model
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Additional details about the error
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }

    /// <summary>
    /// Trace ID for debugging
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}