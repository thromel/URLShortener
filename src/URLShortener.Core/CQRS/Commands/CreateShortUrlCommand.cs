using MediatR;

namespace URLShortener.Core.CQRS.Commands;

public record CreateShortUrlCommand : IRequest<CreateShortUrlResult>
{
    public required string OriginalUrl { get; init; }
    public Guid UserId { get; init; }
    public string? CustomAlias { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string Referrer { get; init; } = string.Empty;
}

public record CreateShortUrlResult
{
    public required string ShortCode { get; init; }
    public required string ShortUrl { get; init; }
    public required string OriginalUrl { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public Guid UserId { get; init; }
    public DateTime? ExpiresAt { get; init; }
}