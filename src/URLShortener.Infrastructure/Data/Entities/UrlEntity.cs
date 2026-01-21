using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Infrastructure.Data.Entities;

public class UrlEntity
{
    public Guid Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? OrganizationId { get; set; }  // Null for personal URLs
    public UrlStatus Status { get; set; }
    public long AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int Version { get; set; }

    // Navigation properties
    public UserEntity? User { get; set; }
    public OrganizationEntity? Organization { get; set; }

    public static UrlEntity FromAggregate(ShortUrlAggregate aggregate)
    {
        return new UrlEntity
        {
            Id = aggregate.Id,
            ShortCode = aggregate.ShortCode,
            OriginalUrl = aggregate.OriginalUrl,
            CreatedAt = aggregate.CreatedAt,
            ExpiresAt = aggregate.ExpiresAt,
            CreatedBy = aggregate.CreatedBy,
            Status = aggregate.Status,
            AccessCount = aggregate.AccessCount,
            LastAccessedAt = aggregate.LastAccessedAt,
            Metadata = aggregate.Metadata,
            Tags = aggregate.Tags,
            Version = aggregate.Version
        };
    }

    public ShortUrlAggregate ToAggregate()
    {
        // This is a simplified reconstruction - in a full event sourcing system,
        // you would reconstruct from events
        return ShortUrlAggregate.FromEvents(new List<DomainEvent>());
    }
}