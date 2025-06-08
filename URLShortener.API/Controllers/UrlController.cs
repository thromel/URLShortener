using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Services;
using URLShortener.API.Models.DTOs;
using URLShortener.Core.CQRS.Commands;
using URLShortener.Core.CQRS.Queries;
using URLShortener.API.Attributes;
using MediatR;
using System.Security.Claims;

namespace URLShortener.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UrlController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<UrlController> _logger;

    public UrlController(
        IMediator mediator,
        IAnalyticsService analyticsService,
        ILogger<UrlController> logger)
    {
        _mediator = mediator;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("UrlCreation")]
    [ProducesResponseType(typeof(CreateUrlResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateUrlDto dto)
    {
        try
        {
            var userId = GetUserId();
            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();

            var command = new CreateShortUrlCommand
            {
                OriginalUrl = dto.OriginalUrl,
                UserId = userId,
                CustomAlias = dto.CustomAlias,
                ExpiresAt = dto.ExpiresAt,
                Metadata = dto.Metadata,
                IpAddress = ipAddress,
                UserAgent = userAgent
            };

            var result = await _mediator.Send(command);
            var shortUrl = $"{Request.Scheme}://{Request.Host}/r/{result.ShortCode}";

            _logger.LogInformation("Created short URL {ShortCode} for user {UserId}", result.ShortCode, userId);

            return Ok(new CreateUrlResponse 
            {
                ShortCode = result.ShortCode,
                ShortUrl = shortUrl,
                OriginalUrl = dto.OriginalUrl,
                UserId = userId,
                ExpiresAt = dto.ExpiresAt
            });
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
    [Cacheable(300, "urlstats")] // Cache for 5 minutes
    [ProducesResponseType(typeof(UrlStatistics), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetUrlStatistics(string shortCode)
    {
        try
        {
            var query = new GetUrlQuery { ShortCode = shortCode };
            var statistics = await _mediator.Send(query);
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
        var userId = GetUserId();
        var ipAddress = GetClientIpAddress();
        var command = new DeleteUrlCommand 
        { 
            ShortCode = shortCode,
            UserId = userId,
            IpAddress = ipAddress
        };
        var deleted = await _mediator.Send(command);

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
        var query = new GetUserUrlsQuery { UserId = userId, Skip = skip, Take = take };
        var urls = await _mediator.Send(query);
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

        var query = new SearchUrlsQuery { SearchTerm = q, Skip = skip, Take = take };
        var urls = await _mediator.Send(query);
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
            var command = new DisableUrlCommand
            {
                ShortCode = shortCode,
                Reason = dto.Reason,
                AdminNotes = dto.AdminNotes
            };
            
            await _mediator.Send(command);
            _logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, dto.Reason);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = $"Short code '{shortCode}' not found" });
        }
    }

    [HttpGet("available/{shortCode}")]
    [Cacheable(60, "availability")] // Cache for 1 minute
    [ProducesResponseType(typeof(AvailabilityResponse), 200)]
    public async Task<IActionResult> CheckAvailability(string shortCode)
    {
        var query = new CheckUrlAvailabilityQuery { ShortCode = shortCode };
        var isAvailable = await _mediator.Send(query);
        return Ok(new { shortCode, available = isAvailable });
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
