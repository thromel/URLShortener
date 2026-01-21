using HotChocolate;
using HotChocolate.Authorization;
using MediatR;
using URLShortener.API.GraphQL.Types;
using URLShortener.Core.CQRS.Queries;
using URLShortener.Core.Interfaces;

namespace URLShortener.API.GraphQL;

/// <summary>
/// GraphQL Query root type for URL Shortener API.
/// </summary>
public class Query
{
    /// <summary>
    /// Gets a URL by its short code.
    /// </summary>
    public async Task<ShortUrlType?> GetUrl(
        string shortCode,
        [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetUrlQuery { ShortCode = shortCode });
        return result != null ? ShortUrlType.FromStatistics(result) : null;
    }

    /// <summary>
    /// Gets detailed statistics for a URL.
    /// </summary>
    public async Task<ShortUrlType?> GetUrlStatistics(
        string shortCode,
        [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetUrlStatisticsQuery { ShortCode = shortCode });
        return result != null ? ShortUrlType.FromStatistics(result) : null;
    }

    /// <summary>
    /// Gets analytics data for a URL.
    /// </summary>
    public async Task<UrlAnalyticsType?> GetUrlAnalytics(
        string shortCode,
        DateTime? startDate,
        DateTime? endDate,
        [Service] IAnalyticsService analyticsService)
    {
        var summary = await analyticsService.GetSummaryAsync(shortCode, startDate, endDate);
        return UrlAnalyticsType.FromSummary(summary);
    }

    /// <summary>
    /// Checks if a short code is available for use.
    /// </summary>
    public async Task<bool> IsShortCodeAvailable(
        string shortCode,
        [Service] IMediator mediator)
    {
        return await mediator.Send(new CheckUrlAvailabilityQuery { ShortCode = shortCode });
    }

    /// <summary>
    /// Gets all URLs for the authenticated user.
    /// </summary>
    [Authorize]
    public async Task<IEnumerable<ShortUrlType>> GetMyUrls(
        PaginationInput? pagination,
        [Service] IMediator mediator,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userIdString = httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            userId = Guid.Empty;
        }

        var skip = pagination?.Skip ?? 0;
        var take = pagination?.Take ?? 20;

        var results = await mediator.Send(new GetUserUrlsQuery
        {
            UserId = userId,
            Skip = skip,
            Take = take
        });
        return results.Select(r => ShortUrlType.FromStatistics(r));
    }

    /// <summary>
    /// Searches URLs by keyword.
    /// </summary>
    [Authorize]
    public async Task<IEnumerable<ShortUrlType>> SearchUrls(
        string searchTerm,
        PaginationInput? pagination,
        [Service] IMediator mediator)
    {
        var skip = pagination?.Skip ?? 0;
        var take = pagination?.Take ?? 20;

        var results = await mediator.Send(new SearchUrlsQuery
        {
            SearchTerm = searchTerm,
            Skip = skip,
            Take = take
        });
        return results.Select(r => ShortUrlType.FromStatistics(r));
    }

    /// <summary>
    /// Gets trending URLs.
    /// </summary>
    public async Task<IEnumerable<TrendingShortUrlType>> GetTrendingUrls(
        [Service] IAnalyticsService analyticsService,
        int count = 10,
        int hoursBack = 24)
    {
        var trending = await analyticsService.GetTrendingUrlsAsync(
            count,
            TimeSpan.FromHours(hoursBack));

        return trending.Select(u => new TrendingShortUrlType
        {
            ShortCode = u.ShortCode,
            OriginalUrl = u.OriginalUrl,
            AccessCount = u.AccessCount,
            TrendScore = u.TrendScore
        });
    }

    /// <summary>
    /// Gets top URLs by access count.
    /// </summary>
    public async Task<IEnumerable<TrendingShortUrlType>> GetTopUrls(
        [Service] IAnalyticsService analyticsService,
        int count = 10,
        int hoursBack = 24)
    {
        var topUrls = await analyticsService.GetTopUrlsAsync(
            count,
            TimeSpan.FromHours(hoursBack));

        return topUrls.Select(u => new TrendingShortUrlType
        {
            ShortCode = u.ShortCode,
            OriginalUrl = u.OriginalUrl,
            AccessCount = u.AccessCount,
            TrendScore = u.TrendScore
        });
    }
}

/// <summary>
/// Type for trending URL results.
/// </summary>
public class TrendingShortUrlType
{
    public string ShortCode { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public long AccessCount { get; set; }
    public double TrendScore { get; set; }
}
