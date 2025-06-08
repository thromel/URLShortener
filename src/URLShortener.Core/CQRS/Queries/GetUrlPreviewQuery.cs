using MediatR;

namespace URLShortener.Core.CQRS.Queries;

public record GetUrlPreviewQuery : IRequest<UrlPreviewResult?>
{
    public required string ShortCode { get; init; }
}

public record UrlPreviewResult
{
    public required string ShortCode { get; init; }
    public required string OriginalUrl { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string? SiteName { get; init; }
    public string? FaviconUrl { get; init; }
    public string? Type { get; init; }
    public DateTime FetchedAt { get; init; }
}