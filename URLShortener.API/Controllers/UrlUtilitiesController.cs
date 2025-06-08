using Microsoft.AspNetCore.Mvc;
using URLShortener.API.Attributes;
using URLShortener.API.Models.DTOs;
using URLShortener.API.Services;
using URLShortener.Core.CQRS.Queries;
using MediatR;
using Asp.Versioning;
using Swashbuckle.AspNetCore.Annotations;

namespace URLShortener.API.Controllers;

/// <summary>
/// Handles URL utility operations like QR codes, previews, and other tools
/// </summary>
[SwaggerTag("Generate QR codes, previews, and other URL utilities")]
public class UrlUtilitiesController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly IQRCodeService _qrCodeService;

    public UrlUtilitiesController(
        IMediator mediator,
        IQRCodeService qrCodeService,
        IClientInfoService clientInfoService,
        ILogger<UrlUtilitiesController> logger)
        : base(clientInfoService, logger)
    {
        _mediator = mediator;
        _qrCodeService = qrCodeService;
    }

    /// <summary>
    /// Generates a QR code for a shortened URL
    /// </summary>
    /// <param name="shortCode">The short code to generate QR code for</param>
    /// <param name="size">Size of the QR code (50-1000, default: 300)</param>
    /// <param name="darkColor">Dark color in hex format (default: black)</param>
    /// <param name="lightColor">Light color in hex format (default: white)</param>
    /// <returns>QR code image as PNG</returns>
    /// <response code="200">Returns the QR code image</response>
    /// <response code="404">If the short code is not found</response>
    /// <response code="400">If the parameters are invalid</response>
    [HttpGet("{shortCode}/qr")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [Produces("image/png")]
    [SwaggerOperation(
        Summary = "Generate QR code for URL",
        Description = "Generates a customizable QR code image for the shortened URL",
        OperationId = "GenerateQRCode",
        Tags = new[] { "URL Utilities" }
    )]
    public async Task<IActionResult> GenerateQRCode(
        string shortCode, 
        [FromQuery] int size = 300,
        [FromQuery] string? darkColor = null,
        [FromQuery] string? lightColor = null)
    {
        // Validate parameters
        if (size < 50 || size > 1000)
        {
            return CreateErrorResponse("Size must be between 50 and 1000 pixels");
        }

        try
        {
            // Check if URL exists
            var query = new GetUrlQuery { ShortCode = shortCode };
            var urlExists = await _mediator.Send(query);
            
            if (urlExists == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            var shortUrl = GenerateShortUrl(shortCode);
            var qrCodeBytes = _qrCodeService.GenerateQRCode(shortUrl, size, darkColor, lightColor);
            
            // Set cache headers for QR codes
            Response.Headers["Cache-Control"] = "public, max-age=3600"; // Cache for 1 hour
            Response.Headers["ETag"] = $"\"{shortCode}-{size}-{darkColor ?? "000000"}-{lightColor ?? "FFFFFF"}\"";
            
            Logger.LogDebug("Generated QR code for {ShortCode} with size {Size}", shortCode, size);
            
            return File(qrCodeBytes, "image/png", $"qr_{shortCode}.png");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate QR code for {ShortCode}", shortCode);
            return CreateErrorResponse("Failed to generate QR code", 500);
        }
    }

    /// <summary>
    /// Gets URL preview with OpenGraph metadata
    /// </summary>
    /// <param name="shortCode">The short code to preview</param>
    /// <param name="refresh">Force refresh of cached preview data</param>
    /// <returns>URL preview information including OpenGraph data</returns>
    /// <response code="200">Returns the URL preview</response>
    /// <response code="404">If the short code is not found</response>
    [HttpGet("{shortCode}/preview")]
    [MapToApiVersion("2.0")]
    [Cacheable(3600, "preview")] // Cache for 1 hour
    [ProducesResponseType(typeof(UrlPreviewResponse), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(
        Summary = "Get URL preview",
        Description = "Retrieves preview information including OpenGraph metadata for the original URL",
        OperationId = "GetUrlPreview",
        Tags = new[] { "URL Utilities" }
    )]
    public async Task<IActionResult> GetUrlPreview(string shortCode, [FromQuery] bool refresh = false)
    {
        try
        {
            var query = new GetUrlPreviewQuery { ShortCode = shortCode };
            var preview = await _mediator.Send(query);
            
            if (preview == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            var response = new UrlPreviewResponse
            {
                ShortCode = preview.ShortCode,
                OriginalUrl = preview.OriginalUrl,
                Title = preview.Title,
                Description = preview.Description,
                ImageUrl = preview.ImageUrl,
                SiteName = preview.SiteName,
                FaviconUrl = preview.FaviconUrl,
                Type = preview.Type,
                FetchedAt = preview.FetchedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get preview for {ShortCode}", shortCode);
            return CreateErrorResponse("Failed to retrieve preview", 500);
        }
    }

    /// <summary>
    /// Generates a batch of QR codes for multiple URLs
    /// </summary>
    /// <param name="request">Batch QR code generation request</param>
    /// <returns>ZIP file containing QR code images</returns>
    /// <response code="200">Returns a ZIP file with QR codes</response>
    /// <response code="400">If the request is invalid</response>
    [HttpPost("qr/batch")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [Produces("application/zip")]
    [SwaggerOperation(
        Summary = "Generate batch QR codes",
        Description = "Generates QR codes for multiple URLs and returns them as a ZIP file",
        OperationId = "GenerateBatchQRCodes",
        Tags = new[] { "URL Utilities" }
    )]
    public async Task<IActionResult> GenerateBatchQRCodes([FromBody] BatchQRCodeRequest request)
    {
        if (request.ShortCodes?.Any() != true)
        {
            return CreateErrorResponse("At least one short code must be provided");
        }

        if (request.ShortCodes.Count > 50)
        {
            return CreateErrorResponse("Maximum 50 QR codes can be generated at once");
        }

        try
        {
            var validUrls = new List<(string shortCode, string shortUrl)>();
            
            // Validate all URLs exist
            foreach (var shortCode in request.ShortCodes)
            {
                var query = new GetUrlQuery { ShortCode = shortCode };
                var urlExists = await _mediator.Send(query);
                
                if (urlExists != null)
                {
                    validUrls.Add((shortCode, GenerateShortUrl(shortCode)));
                }
            }

            if (!validUrls.Any())
            {
                return CreateErrorResponse("No valid short codes found");
            }

            // Create ZIP file with QR codes
            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var (shortCode, shortUrl) in validUrls)
                {
                    var qrCodeBytes = _qrCodeService.GenerateQRCode(shortUrl, request.Size);
                    var entry = archive.CreateEntry($"{shortCode}.png");
                    
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(qrCodeBytes);
                }
            }

            Logger.LogInformation("Generated batch QR codes for {Count} URLs", validUrls.Count);
            
            return File(memoryStream.ToArray(), "application/zip", $"qr_codes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate batch QR codes");
            return CreateErrorResponse("Failed to generate QR codes", 500);
        }
    }

    /// <summary>
    /// Validates URLs for safety and accessibility
    /// </summary>
    /// <param name="shortCode">The short code to validate</param>
    /// <returns>URL validation results</returns>
    /// <response code="200">Returns validation results</response>
    /// <response code="404">If the short code is not found</response>
    [HttpGet("{shortCode}/validate")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(typeof(UrlValidationResponse), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(
        Summary = "Validate URL safety",
        Description = "Checks the target URL for safety, accessibility, and other quality metrics",
        OperationId = "ValidateUrl",
        Tags = new[] { "URL Utilities" }
    )]
    public async Task<IActionResult> ValidateUrl(string shortCode)
    {
        try
        {
            var query = new GetUrlQuery { ShortCode = shortCode };
            var urlInfo = await _mediator.Send(query);
            
            if (urlInfo == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            // In a real implementation, you would perform actual URL validation
            var validationResult = new UrlValidationResponse
            {
                ShortCode = shortCode,
                OriginalUrl = urlInfo.OriginalUrl,
                IsSafe = true, // Placeholder
                IsAccessible = true, // Placeholder
                HttpStatusCode = 200, // Placeholder
                ResponseTime = TimeSpan.FromMilliseconds(150), // Placeholder
                HasSSL = urlInfo.OriginalUrl.StartsWith("https://"),
                ValidatedAt = DateTime.UtcNow
            };

            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to validate URL for {ShortCode}", shortCode);
            return CreateErrorResponse("Failed to validate URL", 500);
        }
    }
}

/// <summary>
/// Request model for batch QR code generation
/// </summary>
public record BatchQRCodeRequest
{
    /// <summary>
    /// List of short codes to generate QR codes for
    /// </summary>
    public List<string> ShortCodes { get; init; } = new();

    /// <summary>
    /// Size of each QR code (default: 300)
    /// </summary>
    public int Size { get; init; } = 300;
}

/// <summary>
/// Response model for URL validation
/// </summary>
public record UrlValidationResponse
{
    public required string ShortCode { get; init; }
    public required string OriginalUrl { get; init; }
    public bool IsSafe { get; init; }
    public bool IsAccessible { get; init; }
    public int HttpStatusCode { get; init; }
    public TimeSpan ResponseTime { get; init; }
    public bool HasSSL { get; init; }
    public DateTime ValidatedAt { get; init; }
}