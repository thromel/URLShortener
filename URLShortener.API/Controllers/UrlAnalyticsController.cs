using Microsoft.AspNetCore.Mvc;
using URLShortener.API.Attributes;
using URLShortener.API.Services;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Domain.Enhanced;
using Asp.Versioning;
using Swashbuckle.AspNetCore.Annotations;

namespace URLShortener.API.Controllers;

/// <summary>
/// Handles URL analytics, statistics, and reporting operations
/// </summary>
[SwaggerTag("View and export URL analytics and statistics")]
public class UrlAnalyticsController : BaseApiController
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IAnalyticsExportService _exportService;

    public UrlAnalyticsController(
        IAnalyticsService analyticsService,
        IAnalyticsExportService exportService,
        IClientInfoService clientInfoService,
        ILogger<UrlAnalyticsController> logger)
        : base(clientInfoService, logger)
    {
        _analyticsService = analyticsService;
        _exportService = exportService;
    }

    /// <summary>
    /// Gets detailed statistics for a shortened URL
    /// </summary>
    /// <param name="shortCode">The short code to retrieve statistics for</param>
    /// <param name="startDate">Start date for analytics range</param>
    /// <param name="endDate">End date for analytics range</param>
    /// <returns>Detailed URL statistics</returns>
    /// <response code="200">Returns the URL statistics</response>
    /// <response code="404">If the short code is not found</response>
    [HttpGet("{shortCode}/statistics")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Cacheable(300, "analytics")] // Cache for 5 minutes
    [ProducesResponseType(typeof(AnalyticsSummary), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(
        Summary = "Get detailed URL statistics",
        Description = "Retrieves comprehensive analytics for a shortened URL with optional date range",
        OperationId = "GetUrlStatistics",
        Tags = new[] { "URL Analytics" }
    )]
    public async Task<IActionResult> GetUrlStatistics(
        string shortCode,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var summary = await _analyticsService.GetSummaryAsync(shortCode, startDate, endDate);
            
            if (summary == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            return Ok(summary);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get statistics for {ShortCode}", shortCode);
            return CreateErrorResponse("Failed to retrieve statistics", 500);
        }
    }

    /// <summary>
    /// Gets top performing URLs
    /// </summary>
    /// <param name="count">Number of URLs to return (max 100)</param>
    /// <param name="timeWindowHours">Time window in hours (default: 24)</param>
    /// <returns>List of top performing URLs</returns>
    /// <response code="200">Returns the top URLs</response>
    [HttpGet("top")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Cacheable(600, "top-urls")] // Cache for 10 minutes
    [ProducesResponseType(typeof(IEnumerable<PopularUrl>), 200)]
    [SwaggerOperation(
        Summary = "Get top performing URLs",
        Description = "Retrieves the most accessed URLs within a time window",
        OperationId = "GetTopUrls",
        Tags = new[] { "URL Analytics" }
    )]
    public async Task<IActionResult> GetTopUrls([FromQuery] int count = 10, [FromQuery] int timeWindowHours = 24)
    {
        if (count > 100) count = 100; // Limit to prevent abuse
        if (timeWindowHours < 1) timeWindowHours = 24;

        var timeWindow = TimeSpan.FromHours(timeWindowHours);
        var topUrls = await _analyticsService.GetTopUrlsAsync(count, timeWindow);
        
        return Ok(topUrls);
    }

    /// <summary>
    /// Gets trending URLs
    /// </summary>
    /// <param name="count">Number of URLs to return (max 100)</param>
    /// <param name="timeWindowHours">Time window in hours (default: 24)</param>
    /// <returns>List of trending URLs</returns>
    /// <response code="200">Returns the trending URLs</response>
    [HttpGet("trending")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Cacheable(600, "trending-urls")] // Cache for 10 minutes
    [ProducesResponseType(typeof(IEnumerable<PopularUrl>), 200)]
    [SwaggerOperation(
        Summary = "Get trending URLs",
        Description = "Retrieves URLs with increasing access patterns",
        OperationId = "GetTrendingUrls",
        Tags = new[] { "URL Analytics" }
    )]
    public async Task<IActionResult> GetTrendingUrls([FromQuery] int count = 10, [FromQuery] int timeWindowHours = 24)
    {
        if (count > 100) count = 100; // Limit to prevent abuse
        if (timeWindowHours < 1) timeWindowHours = 24;

        var timeWindow = TimeSpan.FromHours(timeWindowHours);
        var trendingUrls = await _analyticsService.GetTrendingUrlsAsync(count, timeWindow);
        
        return Ok(trendingUrls);
    }

    /// <summary>
    /// Gets popular URLs by region
    /// </summary>
    /// <param name="region">Region code (e.g., us-east, europe, asia-pacific)</param>
    /// <param name="count">Number of URLs to return (max 100)</param>
    /// <param name="timeWindowHours">Time window in hours (default: 24)</param>
    /// <returns>List of popular URLs in the region</returns>
    /// <response code="200">Returns the regional popular URLs</response>
    [HttpGet("regional/{region}")]
    [MapToApiVersion("2.0")]
    [Cacheable(600, "regional-urls")] // Cache for 10 minutes
    [ProducesResponseType(typeof(IEnumerable<PopularUrl>), 200)]
    [SwaggerOperation(
        Summary = "Get popular URLs by region",
        Description = "Retrieves the most popular URLs in a specific region",
        OperationId = "GetRegionalPopularUrls",
        Tags = new[] { "URL Analytics" }
    )]
    public async Task<IActionResult> GetRegionalPopularUrls(
        string region, 
        [FromQuery] int count = 10, 
        [FromQuery] int timeWindowHours = 24)
    {
        if (count > 100) count = 100; // Limit to prevent abuse
        if (timeWindowHours < 1) timeWindowHours = 24;

        var timeWindow = TimeSpan.FromHours(timeWindowHours);
        var regionalUrls = await _analyticsService.GetRegionalPopularUrlsAsync(region, count, timeWindow);
        
        return Ok(regionalUrls);
    }

    /// <summary>
    /// Exports analytics data for a URL
    /// </summary>
    /// <param name="shortCode">The short code to export analytics for</param>
    /// <param name="format">Export format (csv, json, excel)</param>
    /// <param name="startDate">Start date for analytics range</param>
    /// <param name="endDate">End date for analytics range</param>
    /// <returns>Analytics data in requested format</returns>
    /// <response code="200">Returns the analytics export</response>
    /// <response code="404">If the short code is not found</response>
    /// <response code="400">If the format is not supported</response>
    [HttpGet("{shortCode}/export")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [SwaggerOperation(
        Summary = "Export analytics data",
        Description = "Exports analytics data in CSV, JSON, or Excel format",
        OperationId = "ExportAnalytics",
        Tags = new[] { "URL Analytics" }
    )]
    public async Task<IActionResult> ExportAnalytics(
        string shortCode, 
        [FromQuery] string format = "json",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var summary = await _analyticsService.GetSummaryAsync(shortCode, startDate, endDate);
            
            if (summary == null)
            {
                return CreateNotFoundResponse("Short code", shortCode);
            }

            var formatLower = format.ToLowerInvariant();
            
            return formatLower switch
            {
                "csv" => File(
                    System.Text.Encoding.UTF8.GetBytes(_exportService.GenerateAnalyticsCsv(summary)), 
                    "text/csv", 
                    $"analytics_{shortCode}_{DateTime.UtcNow:yyyyMMdd}.csv"),
                
                "excel" => File(
                    _exportService.GenerateAnalyticsExcel(summary),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"analytics_{shortCode}_{DateTime.UtcNow:yyyyMMdd}.xlsx"),
                
                "json" => Ok(summary),
                
                _ => CreateErrorResponse($"Unsupported format: {format}. Supported formats: csv, json, excel")
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export analytics for {ShortCode}", shortCode);
            return CreateErrorResponse("Failed to export analytics", 500);
        }
    }

    /// <summary>
    /// Streams real-time analytics for a URL
    /// </summary>
    /// <param name="shortCode">The short code to stream analytics for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Real-time analytics stream</returns>
    /// <response code="200">Returns the real-time analytics stream</response>
    [HttpGet("{shortCode}/stream")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [ProducesResponseType(200)]
    [SwaggerOperation(
        Summary = "Stream real-time analytics",
        Description = "Provides a real-time stream of analytics data for a URL",
        OperationId = "StreamAnalytics",
        Tags = new[] { "URL Analytics" }
    )]
    public async IAsyncEnumerable<AnalyticsPoint> StreamAnalytics(
        string shortCode,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting real-time analytics stream for {ShortCode}", shortCode);
        
        await foreach (var point in _analyticsService.StreamAnalyticsAsync(shortCode, cancellationToken))
        {
            yield return point;
        }
        
        Logger.LogInformation("Ended real-time analytics stream for {ShortCode}", shortCode);
    }
}