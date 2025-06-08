using System.ComponentModel.DataAnnotations;
using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.API.Models.DTOs;

/// <summary>
/// Request model for disabling a URL
/// </summary>
public record DisableUrlDto
{
    /// <summary>
    /// Reason for disabling the URL
    /// </summary>
    [Required]
    public DisableReason Reason { get; init; }

    /// <summary>
    /// Optional admin notes about the disable action
    /// </summary>
    [StringLength(500)]
    public string? AdminNotes { get; init; }

    /// <summary>
    /// Whether to notify the URL owner
    /// </summary>
    public bool NotifyOwner { get; init; } = false;
}