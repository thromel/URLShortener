using MediatR;
using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.CQRS.Commands;

public record DisableUrlCommand : IRequest<Unit>
{
    public required string ShortCode { get; init; }
    public DisableReason Reason { get; init; }
    public string? AdminNotes { get; init; }
    public Guid AdminUserId { get; init; }
    public string AdminIpAddress { get; init; } = string.Empty;
}

public record DeleteUrlCommand : IRequest<bool>
{
    public required string ShortCode { get; init; }
    public Guid UserId { get; init; }
    public string IpAddress { get; init; } = string.Empty;
}