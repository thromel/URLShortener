using MediatR;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.CQRS.Queries;

public record GetUrlQuery : IRequest<UrlStatistics?>
{
    public required string ShortCode { get; init; }
}

public record GetOriginalUrlQuery : IRequest<string?>
{
    public required string ShortCode { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string Referrer { get; init; } = string.Empty;
}

public record GetUrlStatisticsQuery : IRequest<UrlStatistics?>
{
    public required string ShortCode { get; init; }
    public Guid? RequestingUserId { get; init; }
}

public record GetUserUrlsQuery : IRequest<IEnumerable<UrlStatistics>>
{
    public Guid UserId { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 50;
}

public record SearchUrlsQuery : IRequest<IEnumerable<UrlStatistics>>
{
    public required string SearchTerm { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 50;
    public Guid? UserId { get; init; }
}

public record CheckUrlAvailabilityQuery : IRequest<bool>
{
    public required string ShortCode { get; init; }
}