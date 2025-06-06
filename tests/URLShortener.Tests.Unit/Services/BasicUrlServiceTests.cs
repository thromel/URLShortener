using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Services;
using Xunit;

namespace URLShortener.Tests.Unit.Services;

public class BasicUrlServiceTests : IDisposable
{
    private readonly Mock<ILogger<BasicUrlService>> _loggerMock;
    private readonly BasicUrlService _service;

    public BasicUrlServiceTests()
    {
        _loggerMock = new Mock<ILogger<BasicUrlService>>();
        _service = new BasicUrlService(_loggerMock.Object);
    }

    public void Dispose()
    {
        // Clear static cache between tests
        var cacheField = typeof(BasicUrlService).GetField("_urlCache", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (cacheField?.GetValue(null) is System.Collections.IDictionary cache)
        {
            cache.Clear();
        }
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnValidShortCode()
    {
        // Arrange
        var request = new CreateUrlRequest(
            OriginalUrl: "https://example.com",
            UserId: Guid.NewGuid()
        );

        // Act
        var result = await _service.CreateShortUrlAsync(request);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(8);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldGenerateUniqueShortCodes()
    {
        // Arrange
        var request1 = new CreateUrlRequest("https://example1.com", Guid.NewGuid());
        var request2 = new CreateUrlRequest("https://example2.com", Guid.NewGuid());

        // Act
        var result1 = await _service.CreateShortUrlAsync(request1);
        var result2 = await _service.CreateShortUrlAsync(request2);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_WithValidShortCode_ShouldReturnOriginalUrl()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var request = new CreateUrlRequest(originalUrl, Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        var result = await _service.GetOriginalUrlAsync(shortCode);

        // Assert
        result.Should().Be(originalUrl);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_WithInvalidShortCode_ShouldReturnNull()
    {
        // Arrange
        var invalidShortCode = "invalid";

        // Act
        var result = await _service.GetOriginalUrlAsync(invalidShortCode);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IsAvailableAsync_WithNewShortCode_ShouldReturnTrue()
    {
        // Arrange
        var shortCode = "newCode";

        // Act
        var result = await _service.IsAvailableAsync(shortCode);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WithExistingShortCode_ShouldReturnFalse()
    {
        // Arrange
        var request = new CreateUrlRequest("https://example.com", Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        var result = await _service.IsAvailableAsync(shortCode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUrlAsync_WithExistingShortCode_ShouldReturnTrue()
    {
        // Arrange
        var request = new CreateUrlRequest("https://example.com", Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        var result = await _service.DeleteUrlAsync(shortCode);

        // Assert
        result.Should().BeTrue();

        // Verify URL is actually deleted
        var originalUrl = await _service.GetOriginalUrlAsync(shortCode);
        originalUrl.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUrlAsync_WithNonExistentShortCode_ShouldReturnFalse()
    {
        // Arrange
        var shortCode = "nonexistent";

        // Act
        var result = await _service.DeleteUrlAsync(shortCode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUrlStatisticsAsync_WithValidShortCode_ShouldReturnStatistics()
    {
        // Arrange
        var originalUrl = "https://example.com";
        var request = new CreateUrlRequest(originalUrl, Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        var result = await _service.GetUrlStatisticsAsync(shortCode);

        // Assert
        result.Should().NotBeNull();
        result.ShortCode.Should().Be(shortCode);
        result.OriginalUrl.Should().Be(originalUrl);
        result.Status.Should().Be(UrlStatus.Active);
        result.AccessCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUrlStatisticsAsync_WithInvalidShortCode_ShouldThrowException()
    {
        // Arrange
        var invalidShortCode = "invalid";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetUrlStatisticsAsync(invalidShortCode));
    }

    [Fact]
    public async Task SearchUrlsAsync_WithMatchingTerm_ShouldReturnMatchingUrls()
    {
        // Arrange
        await _service.CreateShortUrlAsync(new CreateUrlRequest("https://example.com", Guid.NewGuid()));
        await _service.CreateShortUrlAsync(new CreateUrlRequest("https://test.com", Guid.NewGuid()));
        await _service.CreateShortUrlAsync(new CreateUrlRequest("https://another-example.com", Guid.NewGuid()));

        // Act
        var results = await _service.SearchUrlsAsync("example");

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.OriginalUrl.Contains("example")).Should().BeTrue();
    }

    [Fact]
    public async Task SearchUrlsAsync_WithNonMatchingTerm_ShouldReturnEmpty()
    {
        // Arrange
        await _service.CreateShortUrlAsync(new CreateUrlRequest("https://example.com", Guid.NewGuid()));

        // Act
        var results = await _service.SearchUrlsAsync("nonexistent");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAccessAsync_ShouldCompleteWithoutError()
    {
        // Arrange
        var request = new CreateUrlRequest("https://example.com", Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        var act = async () => await _service.RecordAccessAsync(shortCode, "127.0.0.1", "Mozilla/5.0");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisableUrlAsync_ShouldRemoveUrl()
    {
        // Arrange
        var request = new CreateUrlRequest("https://example.com", Guid.NewGuid());
        var shortCode = await _service.CreateShortUrlAsync(request);

        // Act
        await _service.DisableUrlAsync(shortCode, DisableReason.PolicyViolation, "Test disable");

        // Assert
        var originalUrl = await _service.GetOriginalUrlAsync(shortCode);
        originalUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetUserUrlsAsync_ShouldReturnEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _service.GetUserUrlsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }
}