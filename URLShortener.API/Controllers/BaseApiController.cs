using Microsoft.AspNetCore.Mvc;
using URLShortener.API.Services;
using Asp.Versioning;
using Swashbuckle.AspNetCore.Annotations;

namespace URLShortener.API.Controllers;

/// <summary>
/// Base controller providing common functionality for all API controllers
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IClientInfoService ClientInfoService;
    protected readonly ILogger Logger;

    protected BaseApiController(IClientInfoService clientInfoService, ILogger logger)
    {
        ClientInfoService = clientInfoService;
        Logger = logger;
    }

    /// <summary>
    /// Gets the current user ID
    /// </summary>
    protected Guid GetUserId() => ClientInfoService.GetUserId();

    /// <summary>
    /// Gets the client IP address
    /// </summary>
    protected string GetClientIpAddress() => ClientInfoService.GetClientIpAddress();

    /// <summary>
    /// Gets the user agent string
    /// </summary>
    protected string GetUserAgent() => ClientInfoService.GetUserAgent();

    /// <summary>
    /// Gets the referrer URL
    /// </summary>
    protected string GetReferrer() => ClientInfoService.GetReferrer();

    /// <summary>
    /// Generates a short URL from the current request context
    /// </summary>
    protected string GenerateShortUrl(string shortCode)
    {
        return $"{Request.Scheme}://{Request.Host}/r/{shortCode}";
    }

    /// <summary>
    /// Creates a standardized error response
    /// </summary>
    protected IActionResult CreateErrorResponse(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { error = message });
    }

    /// <summary>
    /// Creates a standardized not found response
    /// </summary>
    protected IActionResult CreateNotFoundResponse(string resourceType, string identifier)
    {
        return NotFound(new { error = $"{resourceType} '{identifier}' not found" });
    }
}