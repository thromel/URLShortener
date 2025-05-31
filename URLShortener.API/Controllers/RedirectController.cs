using Microsoft.AspNetCore.Mvc;
using URLShortener.Core.Interfaces;

namespace URLShortener.API.Controllers;

[ApiController]
[Route("r")]
public class RedirectController : ControllerBase
{
    private readonly IUrlService _urlService;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(IUrlService urlService, ILogger<RedirectController> logger)
    {
        _urlService = urlService;
        _logger = logger;
    }

    [HttpGet("{shortCode}")]
    [ProducesResponseType(302)]
    [ProducesResponseType(404)]
    [ProducesResponseType(410)] // Gone - URL expired
    public async Task<IActionResult> RedirectToUrl(string shortCode)
    {
        try
        {
            var originalUrl = await _urlService.GetOriginalUrlAsync(shortCode);

            if (string.IsNullOrEmpty(originalUrl))
            {
                _logger.LogWarning("Short code not found: {ShortCode}", shortCode);
                return NotFound($"Short URL '{shortCode}' not found");
            }

            // Record the access for analytics (fire and forget)
            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers.UserAgent.ToString();
            var referrer = Request.Headers.Referer.ToString();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _urlService.RecordAccessAsync(shortCode, ipAddress, userAgent, referrer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record access for {ShortCode}", shortCode);
                }
            });

            _logger.LogInformation("Redirecting {ShortCode} to {OriginalUrl} from {IpAddress}",
                shortCode, originalUrl, ipAddress);

            return Redirect(originalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing redirect for {ShortCode}", shortCode);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpHead("{shortCode}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CheckUrl(string shortCode)
    {
        var originalUrl = await _urlService.GetOriginalUrlAsync(shortCode);

        if (string.IsNullOrEmpty(originalUrl))
        {
            return NotFound();
        }

        Response.Headers.Add("X-Original-URL", originalUrl);
        return Ok();
    }

    [HttpGet("{shortCode}/preview")]
    [ProducesResponseType(typeof(PreviewResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PreviewUrl(string shortCode)
    {
        try
        {
            var statistics = await _urlService.GetUrlStatisticsAsync(shortCode);

            var preview = new PreviewResponse(
                ShortCode: shortCode,
                OriginalUrl: statistics.OriginalUrl,
                CreatedAt: statistics.CreatedAt,
                AccessCount: statistics.AccessCount,
                ShortUrl: $"{Request.Scheme}://{Request.Host}/r/{shortCode}"
            );

            return Ok(preview);
        }
        catch (ArgumentException)
        {
            return NotFound($"Short URL '{shortCode}' not found");
        }
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

public record PreviewResponse(
    string ShortCode,
    string OriginalUrl,
    DateTime CreatedAt,
    long AccessCount,
    string ShortUrl
);