using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Domain.Enhanced;
using System.Security.Claims;

namespace URLShortener.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UrlController : ControllerBase
{
    private readonly IUrlService _urlService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<UrlController> _logger;

    public UrlController(
        IUrlService urlService,
        IAnalyticsService analyticsService,
        ILogger<UrlController> logger)
    {
        _urlService = urlService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateUrlResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateUrlDto dto)
    {
        try
        {
            var userId = GetUserId();
            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();

            var request = new CreateUrlRequest(
                OriginalUrl: dto.OriginalUrl,
                UserId: userId,
                CustomAlias: dto.CustomAlias,
                ExpiresAt: dto.ExpiresAt,
                Metadata: dto.Metadata,
                IpAddress: ipAddress,
                UserAgent: userAgent
            );

            var shortCode = await _urlService.CreateShortUrlAsync(request);
            var shortUrl = $"{Request.Scheme}://{Request.Host}/r/{shortCode}";

            _logger.LogInformation("Created short URL {ShortCode} for user {UserId}", shortCode, userId);

            return CreatedAtAction(
                nameof(GetUrlStatistics),
                new { shortCode },
                new CreateUrlResponse(shortCode, shortUrl, dto.OriginalUrl));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{shortCode}")]
    [ProducesResponseType(typeof(UrlStatistics), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetUrlStatistics(string shortCode)
    {
        try
        {
            var statistics = await _urlService.GetUrlStatisticsAsync(shortCode);
            return Ok(statistics);
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = $"Short code '{shortCode}' not found" });
        }
    }

    [HttpDelete("{shortCode}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUrl(string shortCode)
    {
        var deleted = await _urlService.DeleteUrlAsync(shortCode);

        if (!deleted)
        {
            return NotFound(new { error = $"Short code '{shortCode}' not found" });
        }

        _logger.LogInformation("Deleted URL {ShortCode}", shortCode);
        return NoContent();
    }

    [HttpGet("my-urls")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<UrlStatistics>), 200)]
    public async Task<IActionResult> GetMyUrls([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = GetUserId();
        var urls = await _urlService.GetUserUrlsAsync(userId, skip, take);
        return Ok(urls);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<UrlStatistics>), 200)]
    public async Task<IActionResult> SearchUrls([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Search query cannot be empty" });
        }

        var urls = await _urlService.SearchUrlsAsync(q, skip, take);
        return Ok(urls);
    }

    [HttpPost("{shortCode}/disable")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DisableUrl(string shortCode, [FromBody] DisableUrlDto dto)
    {
        try
        {
            await _urlService.DisableUrlAsync(shortCode, dto.Reason, dto.AdminNotes);
            _logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, dto.Reason);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = $"Short code '{shortCode}' not found" });
        }
    }

    [HttpGet("{shortCode}/check")]
    [ProducesResponseType(typeof(AvailabilityResponse), 200)]
    public async Task<IActionResult> CheckAvailability(string shortCode)
    {
        var isAvailable = await _urlService.IsAvailableAsync(shortCode);
        return Ok(new AvailabilityResponse(shortCode, isAvailable));
    }

    [HttpGet("{shortCode}/analytics/real-time")]
    [ProducesResponseType(200)]
    public async IAsyncEnumerable<AnalyticsPoint> GetRealTimeAnalytics(
        string shortCode,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var point in _analyticsService.StreamAnalyticsAsync(shortCode, cancellationToken))
        {
            yield return point;
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        // For demo purposes, return a default user ID
        // In production, this should be properly authenticated
        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    private string GetClientIpAddress()
    {
        // Try to get the real IP address from headers (for load balancers/proxies)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public record CreateUrlDto(
    string OriginalUrl,
    string? CustomAlias = null,
    DateTime? ExpiresAt = null,
    Dictionary<string, string>? Metadata = null
);

public record CreateUrlResponse(
    string ShortCode,
    string ShortUrl,
    string OriginalUrl
);

public record DisableUrlDto(
    DisableReason Reason,
    string? AdminNotes = null
);

public record AvailabilityResponse(
    string ShortCode,
    bool IsAvailable
);