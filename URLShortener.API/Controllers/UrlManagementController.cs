using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using URLShortener.API.Models.DTOs;
using URLShortener.API.Services;
using URLShortener.Core.CQRS.Commands;
using URLShortener.Core.CQRS.Queries;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using MediatR;
using Asp.Versioning;
using Swashbuckle.AspNetCore.Annotations;

namespace URLShortener.API.Controllers;

/// <summary>
/// Handles URL creation, deletion, and basic management operations
/// </summary>
[SwaggerTag("Create, update, and manage shortened URLs")]
public class UrlManagementController : BaseApiController
{
    private readonly IMediator _mediator;

    public UrlManagementController(
        IMediator mediator,
        IClientInfoService clientInfoService,
        ILogger<UrlManagementController> logger)
        : base(clientInfoService, logger)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new shortened URL
    /// </summary>
    /// <param name="dto">URL creation details</param>
    /// <returns>The created short URL details</returns>
    /// <response code="201">Returns the newly created short URL</response>
    /// <response code="400">If the URL is invalid</response>
    /// <response code="409">If the custom alias already exists</response>
    /// <response code="429">If rate limit is exceeded</response>
    [HttpPost]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [EnableRateLimiting("UrlCreation")]
    [ProducesResponseType(typeof(CreateUrlResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(429)]
    [SwaggerOperation(
        Summary = "Create a shortened URL",
        Description = "Creates a new shortened URL with optional custom alias and expiration",
        OperationId = "CreateShortUrl",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateUrlDto dto)
    {
        try
        {
            var userId = GetUserId();
            var command = new CreateShortUrlCommand
            {
                OriginalUrl = dto.OriginalUrl,
                UserId = userId,
                CustomAlias = dto.CustomAlias,
                ExpiresAt = dto.ExpiresAt,
                Metadata = dto.Metadata,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent()
            };

            var result = await _mediator.Send(command);
            var shortUrl = GenerateShortUrl(result.ShortCode);

            Logger.LogInformation("Created short URL {ShortCode} for user {UserId}", result.ShortCode, userId);

            var response = new CreateUrlResponse 
            {
                ShortCode = result.ShortCode,
                ShortUrl = shortUrl,
                OriginalUrl = dto.OriginalUrl,
                UserId = userId,
                ExpiresAt = dto.ExpiresAt
            };

            return CreatedAtAction(nameof(GetUrlInfo), new { shortCode = result.ShortCode }, response);
        }
        catch (ArgumentException ex)
        {
            return CreateErrorResponse(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return CreateErrorResponse(ex.Message, 409);
        }
    }

    /// <summary>
    /// Gets basic information about a shortened URL
    /// </summary>
    /// <param name="shortCode">The short code to retrieve information for</param>
    /// <returns>URL information</returns>
    /// <response code="200">Returns the URL information</response>
    /// <response code="404">If the short code is not found</response>
    [HttpGet("{shortCode}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(typeof(UrlStatistics), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(
        Summary = "Get URL information",
        Description = "Retrieves basic information about a shortened URL",
        OperationId = "GetUrlInfo",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> GetUrlInfo(string shortCode)
    {
        try
        {
            var query = new GetUrlQuery { ShortCode = shortCode };
            var urlInfo = await _mediator.Send(query);
            
            if (urlInfo == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            return Ok(urlInfo);
        }
        catch (ArgumentException)
        {
            return CreateNotFoundResponse("Short code", shortCode);
        }
    }

    /// <summary>
    /// Deletes a shortened URL
    /// </summary>
    /// <param name="shortCode">The short code to delete</param>
    /// <returns>No content on successful deletion</returns>
    /// <response code="204">URL deleted successfully</response>
    /// <response code="404">If the short code is not found</response>
    /// <response code="401">If not authenticated</response>
    [HttpDelete("{shortCode}")]
    [Authorize]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [SwaggerOperation(
        Summary = "Delete a shortened URL",
        Description = "Deletes a shortened URL (user can only delete their own URLs)",
        OperationId = "DeleteUrl",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> DeleteUrl(string shortCode)
    {
        var userId = GetUserId();
        var command = new DeleteUrlCommand 
        { 
            ShortCode = shortCode,
            UserId = userId,
            IpAddress = GetClientIpAddress()
        };
        
        var deleted = await _mediator.Send(command);

        if (!deleted)
        {
            return CreateNotFoundResponse("Short code", shortCode);
        }

        Logger.LogInformation("Deleted URL {ShortCode} by user {UserId}", shortCode, userId);
        return NoContent();
    }

    /// <summary>
    /// Gets URLs created by the current user
    /// </summary>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to take (max 100)</param>
    /// <returns>List of user's URLs</returns>
    /// <response code="200">Returns the list of URLs</response>
    /// <response code="401">If not authenticated</response>
    [HttpGet("my-urls")]
    [Authorize]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(typeof(IEnumerable<UrlStatistics>), 200)]
    [ProducesResponseType(401)]
    [SwaggerOperation(
        Summary = "Get user's URLs",
        Description = "Retrieves URLs created by the current user with pagination",
        OperationId = "GetMyUrls",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> GetMyUrls([FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (take > 100) take = 100; // Limit to prevent abuse
        
        var userId = GetUserId();
        var query = new GetUserUrlsQuery { UserId = userId, Skip = skip, Take = take };
        var urls = await _mediator.Send(query);
        
        return Ok(urls);
    }

    /// <summary>
    /// Searches for URLs based on a query string
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to take (max 100)</param>
    /// <returns>List of matching URLs</returns>
    /// <response code="200">Returns the search results</response>
    /// <response code="400">If the search query is invalid</response>
    [HttpGet("search")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(typeof(IEnumerable<UrlStatistics>), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(
        Summary = "Search URLs",
        Description = "Searches for URLs based on a query string",
        OperationId = "SearchUrls",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> SearchUrls([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return CreateErrorResponse("Search query cannot be empty");
        }

        if (take > 100) take = 100; // Limit to prevent abuse

        var query = new SearchUrlsQuery { SearchTerm = q, Skip = skip, Take = take };
        var urls = await _mediator.Send(query);
        
        return Ok(urls);
    }

    /// <summary>
    /// Checks if a short code is available for use
    /// </summary>
    /// <param name="shortCode">The short code to check</param>
    /// <returns>Availability status</returns>
    /// <response code="200">Returns availability status</response>
    [HttpGet("available/{shortCode}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(
        Summary = "Check short code availability",
        Description = "Checks if a custom short code is available for use",
        OperationId = "CheckAvailability",
        Tags = new[] { "URL Management" }
    )]
    public async Task<IActionResult> CheckAvailability(string shortCode)
    {
        var query = new CheckUrlAvailabilityQuery { ShortCode = shortCode };
        var isAvailable = await _mediator.Send(query);
        
        return Ok(new { shortCode, available = isAvailable });
    }

    /// <summary>
    /// Disables a URL (Admin only)
    /// </summary>
    /// <param name="shortCode">The short code to disable</param>
    /// <param name="dto">Disable details</param>
    /// <returns>No content on successful disable</returns>
    /// <response code="204">URL disabled successfully</response>
    /// <response code="404">If the short code is not found</response>
    /// <response code="403">If not authorized</response>
    [HttpPost("{shortCode}/disable")]
    [Authorize(Roles = "Admin")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(403)]
    [SwaggerOperation(
        Summary = "Disable a URL",
        Description = "Disables a URL for policy violations (Admin only)",
        OperationId = "DisableUrl",
        Tags = new[] { "URL Management" }
    )]
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
            Logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, dto.Reason);
            
            return NoContent();
        }
        catch (ArgumentException)
        {
            return CreateNotFoundResponse("Short code", shortCode);
        }
    }
}