using FluentAssertions;
using URLShortener.Core.Domain.Enhanced;
using Xunit;

namespace URLShortener.Tests.Unit.Domain;

public class ShortUrlAggregateTests
{
    [Fact]
    public void Create_WithValidUrl_ShouldCreateAggregate()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();

        // Act
        var aggregate = ShortUrlAggregate.Create(originalUrl, userId);

        // Assert
        aggregate.Should().NotBeNull();
        aggregate.Id.Should().NotBeEmpty();
        aggregate.OriginalUrl.Should().Be(originalUrl);
        aggregate.CreatedBy.Should().Be(userId);
        aggregate.Status.Should().Be(UrlStatus.Active);
        aggregate.ShortCode.Should().NotBeNullOrEmpty();
        aggregate.AccessCount.Should().Be(0);
        aggregate.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithCustomAlias_ShouldUseCustomAlias()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();
        var customAlias = "mylink";

        // Act
        var aggregate = ShortUrlAggregate.Create(originalUrl, userId, customAlias: customAlias);

        // Assert
        aggregate.ShortCode.Should().Be(customAlias);
    }

    [Fact]
    public void Create_WithExpirationDate_ShouldSetExpirationDate()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var aggregate = ShortUrlAggregate.Create(originalUrl, userId, expiresAt: expiresAt);

        // Assert
        aggregate.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithInvalidUrl_ShouldThrowException()
    {
        // Arrange
        var invalidUrl = "not-a-url";
        var userId = Guid.NewGuid();

        // Act & Assert
        var act = () => ShortUrlAggregate.Create(invalidUrl, userId);
        act.Should().Throw<ArgumentException>().WithMessage("Invalid URL format*");
    }

    [Fact]
    public void Create_WithPastExpirationDate_ShouldThrowException()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddDays(-1);

        // Act & Assert
        var act = () => ShortUrlAggregate.Create(originalUrl, userId, expiresAt: pastDate);
        act.Should().Throw<ArgumentException>().WithMessage("Expiration date must be in the future*");
    }

    [Fact]
    public void RecordAccess_WithActiveUrl_ShouldIncrementAccessCount()
    {
        // Arrange
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());
        var ipAddress = "127.0.0.1";
        var userAgent = "Mozilla/5.0";
        var referrer = "https://google.com";
        var location = new GeoLocation("US", "CA", "San Francisco", 37.7749, -122.4194);
        var deviceInfo = new DeviceInfo("Desktop", "Chrome", "Windows", false);

        // Act
        aggregate.RecordAccess(ipAddress, userAgent, referrer, location, deviceInfo);

        // Assert
        aggregate.AccessCount.Should().Be(1);
        aggregate.LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordAccess_WithDisabledUrl_ShouldThrowException()
    {
        // Arrange
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());
        aggregate.Disable(DisableReason.AdminAction);
        
        var ipAddress = "127.0.0.1";
        var userAgent = "Mozilla/5.0";
        var referrer = "https://google.com";
        var location = new GeoLocation("US", "CA", "San Francisco", 37.7749, -122.4194);
        var deviceInfo = new DeviceInfo("Desktop", "Chrome", "Windows", false);

        // Act & Assert
        var act = () => aggregate.RecordAccess(ipAddress, userAgent, referrer, location, deviceInfo);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Cannot access URL with status: Disabled");
    }

    [Fact]
    public void RecordAccess_WithExpiredUrl_ShouldExpireUrl()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddMilliseconds(100);
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid(), expiresAt: expiresAt);
        
        // Wait for expiration
        Thread.Sleep(200);
        
        var ipAddress = "127.0.0.1";
        var userAgent = "Mozilla/5.0";
        var referrer = "https://google.com";
        var location = new GeoLocation("US", "CA", "San Francisco", 37.7749, -122.4194);
        var deviceInfo = new DeviceInfo("Desktop", "Chrome", "Windows", false);

        // Act
        aggregate.RecordAccess(ipAddress, userAgent, referrer, location, deviceInfo);

        // Assert
        aggregate.Status.Should().Be(UrlStatus.Expired);
        aggregate.AccessCount.Should().Be(0); // Access wasn't recorded due to expiration
    }

    [Fact]
    public void Disable_WithActiveUrl_ShouldDisableUrl()
    {
        // Arrange
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());
        var reason = DisableReason.PolicyViolation;
        var adminNotes = "Contains inappropriate content";

        // Act
        aggregate.Disable(reason, adminNotes);

        // Assert
        aggregate.Status.Should().Be(UrlStatus.Disabled);
    }

    [Fact]
    public void Disable_WithAlreadyDisabledUrl_ShouldNotChangeStatus()
    {
        // Arrange
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());
        aggregate.Disable(DisableReason.AdminAction);
        var originalEventCount = aggregate.GetUncommittedEvents().Count;

        // Act
        aggregate.Disable(DisableReason.PolicyViolation);

        // Assert
        aggregate.Status.Should().Be(UrlStatus.Disabled);
        aggregate.GetUncommittedEvents().Count.Should().Be(originalEventCount); // No new events added
    }

    [Fact]
    public void GetUncommittedEvents_AfterCreation_ShouldContainCreatedEvent()
    {
        // Arrange & Act
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());

        // Assert
        var events = aggregate.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events.First().Should().BeOfType<UrlCreatedEvent>();
    }

    [Fact]
    public void GetUncommittedEvents_AfterAccess_ShouldContainAccessEvent()
    {
        // Arrange
        var aggregate = ShortUrlAggregate.Create("https://example.com", Guid.NewGuid());
        var location = new GeoLocation("US", "CA", "San Francisco", 37.7749, -122.4194);
        var deviceInfo = new DeviceInfo("Desktop", "Chrome", "Windows", false);

        // Act
        aggregate.RecordAccess("127.0.0.1", "Mozilla/5.0", "https://google.com", location, deviceInfo);

        // Assert
        var events = aggregate.GetUncommittedEvents();
        events.Should().HaveCount(2);
        events.Last().Should().BeOfType<UrlAccessedEvent>();
    }

    [Fact]
    public void FromEvents_WithValidEventSequence_ShouldReconstructAggregate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var aggregateId = Guid.NewGuid();
        var shortCode = "abc123";
        var originalUrl = "https://example.com";
        
        var events = new List<DomainEvent>
        {
            new UrlCreatedEvent(
                aggregateId, 
                DateTime.UtcNow.AddHours(-1), 
                Guid.NewGuid(),
                shortCode,
                originalUrl,
                null,
                userId,
                "127.0.0.1",
                "Mozilla/5.0",
                null,
                new Dictionary<string, string>()) { Version = 1 },
            new UrlAccessedEvent(
                aggregateId,
                DateTime.UtcNow.AddMinutes(-30),
                Guid.NewGuid(),
                shortCode,
                "192.168.1.1",
                "Mozilla/5.0",
                "https://google.com",
                new GeoLocation("US", "CA", "San Francisco", 37.7749, -122.4194),
                new DeviceInfo("Desktop", "Chrome", "Windows", false)) { Version = 2 }
        };

        // Act
        var aggregate = ShortUrlAggregate.FromEvents(events);

        // Assert
        aggregate.Id.Should().Be(aggregateId);
        aggregate.ShortCode.Should().Be(shortCode);
        aggregate.OriginalUrl.Should().Be(originalUrl);
        aggregate.CreatedBy.Should().Be(userId);
        aggregate.AccessCount.Should().Be(1);
        aggregate.Status.Should().Be(UrlStatus.Active);
        aggregate.Version.Should().Be(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullUrl_ShouldThrowException(string invalidUrl)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        var act = () => ShortUrlAggregate.Create(invalidUrl, userId);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldGenerateUniqueShortCodes()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();

        // Act
        var aggregate1 = ShortUrlAggregate.Create(originalUrl, userId);
        var aggregate2 = ShortUrlAggregate.Create(originalUrl, userId);

        // Assert
        aggregate1.ShortCode.Should().NotBe(aggregate2.ShortCode);
    }

    [Fact]
    public void Create_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var userId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            { "campaign", "summer2024" },
            { "source", "email" }
        };

        // Act
        var aggregate = ShortUrlAggregate.Create(originalUrl, userId, metadata: metadata);

        // Assert
        aggregate.Metadata.Should().BeEquivalentTo(metadata);
    }
}