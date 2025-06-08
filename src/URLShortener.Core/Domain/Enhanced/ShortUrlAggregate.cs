using System.Collections.Concurrent;
using System.Text;

namespace URLShortener.Core.Domain.Enhanced;

public abstract record DomainEvent(Guid AggregateId, DateTime OccurredAt, Guid EventId)
{
    public int Version { get; init; }
}

public record UrlCreatedEvent(
    Guid AggregateId,
    DateTime OccurredAt,
    Guid EventId,
    string ShortCode,
    string OriginalUrl,
    string? CustomAlias,
    Guid UserId,
    string IpAddress,
    string UserAgent,
    DateTime? ExpiresAt,
    Dictionary<string, string> Metadata
) : DomainEvent(AggregateId, OccurredAt, EventId);

public record UrlAccessedEvent(
    Guid AggregateId,
    DateTime OccurredAt,
    Guid EventId,
    string ShortCode,
    string IpAddress,
    string UserAgent,
    string Referrer,
    GeoLocation Location,
    DeviceInfo DeviceInfo
) : DomainEvent(AggregateId, OccurredAt, EventId);

public record UrlExpiredEvent(
    Guid AggregateId,
    DateTime OccurredAt,
    Guid EventId,
    string ShortCode,
    DateTime ExpiredAt
) : DomainEvent(AggregateId, OccurredAt, EventId);

public record UrlDisabledEvent(
    Guid AggregateId,
    DateTime OccurredAt,
    Guid EventId,
    string ShortCode,
    DisableReason Reason,
    string? AdminNotes
) : DomainEvent(AggregateId, OccurredAt, EventId);

public record GeoLocation(string Country, string Region, string City, double Latitude, double Longitude);
public record DeviceInfo(string DeviceType, string Browser, string OperatingSystem, bool IsMobile);

public enum UrlStatus
{
    Active,
    Expired,
    Disabled,
    Suspended
}

public enum DisableReason
{
    AdminAction,
    PolicyViolation,
    SuspiciousActivity,
    Copyright,
    Spam
}

public abstract class AggregateRoot
{
    public Guid Id { get; protected set; }
    public int Version { get; protected set; }

    private readonly List<DomainEvent> _uncommittedEvents = new();

    public IReadOnlyList<DomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();

    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    protected void AddEvent(DomainEvent @event)
    {
        _uncommittedEvents.Add(@event with { Version = Version + 1 });
        Version++;
    }
}

public class ShortUrlAggregate : AggregateRoot
{
    private static readonly ConcurrentDictionary<string, byte> _usedCodes = new();
    private static long _sequence = 0;
    private static readonly object _lock = new();

    public string ShortCode { get; private set; } = string.Empty;
    public string OriginalUrl { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public UrlStatus Status { get; private set; }
    public long AccessCount { get; private set; }
    public DateTime? LastAccessedAt { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public List<string> Tags { get; private set; } = new();

    // For event sourcing reconstruction
    private ShortUrlAggregate() { }

    public static ShortUrlAggregate Create(
        string originalUrl,
        Guid userId,
        string? customAlias = null,
        DateTime? expiresAt = null,
        string ipAddress = "",
        string userAgent = "",
        Dictionary<string, string>? metadata = null)
    {
        var aggregate = new ShortUrlAggregate();
        var shortCode = customAlias ?? GenerateOptimizedShortCode();

        // Validate inputs
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(originalUrl));

        if (customAlias != null && _usedCodes.ContainsKey(customAlias))
            throw new InvalidOperationException("Custom alias already exists");

        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future", nameof(expiresAt));

        var @event = new UrlCreatedEvent(
            AggregateId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            EventId: Guid.NewGuid(),
            ShortCode: shortCode,
            OriginalUrl: originalUrl,
            CustomAlias: customAlias,
            UserId: userId,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            ExpiresAt: expiresAt,
            Metadata: metadata ?? new Dictionary<string, string>()
        );

        aggregate.Apply(@event);
        aggregate.AddEvent(@event);

        // Track used code
        _usedCodes.TryAdd(shortCode, 0);

        return aggregate;
    }

    public void RecordAccess(string ipAddress, string userAgent, string referrer, GeoLocation location, DeviceInfo deviceInfo)
    {
        if (Status != UrlStatus.Active)
            throw new InvalidOperationException($"Cannot access URL with status: {Status}");

        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value)
        {
            var expiredEvent = new UrlExpiredEvent(
                AggregateId: Id,
                OccurredAt: DateTime.UtcNow,
                EventId: Guid.NewGuid(),
                ShortCode: ShortCode,
                ExpiredAt: ExpiresAt.Value
            );

            Apply(expiredEvent);
            AddEvent(expiredEvent);
            return;
        }

        var @event = new UrlAccessedEvent(
            AggregateId: Id,
            OccurredAt: DateTime.UtcNow,
            EventId: Guid.NewGuid(),
            ShortCode: ShortCode,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            Referrer: referrer,
            Location: location,
            DeviceInfo: deviceInfo
        );

        Apply(@event);
        AddEvent(@event);
    }

    public void Disable(DisableReason reason, string? adminNotes = null)
    {
        if (Status == UrlStatus.Disabled)
            return;

        var @event = new UrlDisabledEvent(
            AggregateId: Id,
            OccurredAt: DateTime.UtcNow,
            EventId: Guid.NewGuid(),
            ShortCode: ShortCode,
            Reason: reason,
            AdminNotes: adminNotes
        );

        Apply(@event);
        AddEvent(@event);
    }

    // Event application methods
    private void Apply(UrlCreatedEvent @event)
    {
        Id = @event.AggregateId;
        ShortCode = @event.ShortCode;
        OriginalUrl = @event.OriginalUrl;
        CreatedAt = @event.OccurredAt;
        ExpiresAt = @event.ExpiresAt;
        CreatedBy = @event.UserId;
        Status = UrlStatus.Active;
        AccessCount = 0;
        Metadata = @event.Metadata;
    }

    private void Apply(UrlAccessedEvent @event)
    {
        AccessCount++;
        LastAccessedAt = @event.OccurredAt;
    }

    private void Apply(UrlExpiredEvent @event)
    {
        Status = UrlStatus.Expired;
    }

    private void Apply(UrlDisabledEvent @event)
    {
        Status = UrlStatus.Disabled;
    }

    // Reconstruct aggregate from events
    public static ShortUrlAggregate FromEvents(IEnumerable<DomainEvent> events)
    {
        var aggregate = new ShortUrlAggregate();

        foreach (var @event in events.OrderBy(e => e.Version))
        {
            aggregate.ApplyEvent(@event);
            aggregate.Version = @event.Version;
        }

        return aggregate;
    }

    private void ApplyEvent(DomainEvent @event)
    {
        switch (@event)
        {
            case UrlCreatedEvent e:
                Apply(e);
                break;
            case UrlAccessedEvent e:
                Apply(e);
                break;
            case UrlExpiredEvent e:
                Apply(e);
                break;
            case UrlDisabledEvent e:
                Apply(e);
                break;
            default:
                throw new ArgumentException($"Unknown event type: {@event.GetType().Name}");
        }
    }

    // Advanced short code generation with collision avoidance
    private static string GenerateOptimizedShortCode()
    {
        lock (_lock)
        {
            // Snowflake-like algorithm for guaranteed uniqueness
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var machineId = Environment.MachineName.GetHashCode() & 0x3FF; // 10 bits
            var sequence = Interlocked.Increment(ref _sequence) & 0xFFF; // 12 bits

            var id = (timestamp << 22) | ((uint)machineId << 12) | (uint)sequence;
            var shortCode = Base62.Encode(id);

            // Ensure no collision (extremely rare but possible)
            while (_usedCodes.ContainsKey(shortCode))
            {
                sequence = Interlocked.Increment(ref _sequence) & 0xFFF;
                id = (timestamp << 22) | ((uint)machineId << 12) | (uint)sequence;
                shortCode = Base62.Encode(id);
            }

            return shortCode;
        }
    }
}

public static class Base62
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly int Base = Alphabet.Length;

    public static string Encode(long value)
    {
        if (value == 0) return "0";

        var result = new StringBuilder();

        while (value > 0)
        {
            result.Insert(0, Alphabet[(int)(value % Base)]);
            value /= Base;
        }

        return result.ToString();
    }

    public static long Decode(string encoded)
    {
        var result = 0L;
        var power = 1L;

        for (var i = encoded.Length - 1; i >= 0; i--)
        {
            var index = Alphabet.IndexOf(encoded[i]);
            if (index == -1)
                throw new ArgumentException($"Invalid character in encoded string: {encoded[i]}");

            result += index * power;
            power *= Base;
        }

        return result;
    }
}