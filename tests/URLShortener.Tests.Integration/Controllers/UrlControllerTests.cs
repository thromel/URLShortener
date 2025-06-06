using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using URLShortener.Infrastructure.Data;
using Xunit;

namespace URLShortener.Tests.Integration.Controllers;

public class UrlControllerTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory<Program> _factory;

    public UrlControllerTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateShortUrl_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = new
        {
            originalUrl = "https://example.com",
            customAlias = "",
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify response is a valid short code
        var jsonDoc = JsonDocument.Parse(content);
        var shortCode = jsonDoc.RootElement.GetProperty("shortCode").GetString();
        shortCode.Should().NotBeNullOrEmpty();
        shortCode.Should().HaveLength(8); // Basic service generates 8-char codes
    }

    [Fact]
    public async Task CreateShortUrl_WithInvalidUrl_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            originalUrl = "not-a-valid-url",
            customAlias = "",
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateShortUrl_WithCustomAlias_ShouldUseCustomAlias()
    {
        // Arrange
        var customAlias = "my-custom-link";
        var request = new
        {
            originalUrl = "https://example.com",
            customAlias = customAlias,
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/url", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        var shortCode = jsonDoc.RootElement.GetProperty("shortCode").GetString();
        shortCode.Should().Be(customAlias);
    }

    [Fact]
    public async Task GetUrlStatistics_WithValidShortCode_ShouldReturnStatistics()
    {
        // Arrange
        var createRequest = new
        {
            originalUrl = "https://example.com",
            customAlias = "",
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        var createResponse = await _client.PostAsJsonAsync("/api/url", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJsonDoc = JsonDocument.Parse(createContent);
        var shortCode = createJsonDoc.RootElement.GetProperty("shortCode").GetString();

        // Act
        var response = await _client.GetAsync($"/api/url/{shortCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        
        var statistics = jsonDoc.RootElement;
        statistics.GetProperty("shortCode").GetString().Should().Be(shortCode);
        statistics.GetProperty("originalUrl").GetString().Should().Be("https://example.com");
        statistics.GetProperty("accessCount").GetInt32().Should().Be(0);
        statistics.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task GetUrlStatistics_WithInvalidShortCode_ShouldReturnNotFound()
    {
        // Arrange
        var invalidShortCode = "invalid123";

        // Act
        var response = await _client.GetAsync($"/api/url/{invalidShortCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUrl_WithValidShortCode_ShouldReturnSuccess()
    {
        // Arrange
        var createRequest = new
        {
            originalUrl = "https://example.com",
            customAlias = "",
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        var createResponse = await _client.PostAsJsonAsync("/api/url", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createJsonDoc = JsonDocument.Parse(createContent);
        var shortCode = createJsonDoc.RootElement.GetProperty("shortCode").GetString();

        // Act
        var response = await _client.DeleteAsync($"/api/url/{shortCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify URL is actually deleted
        var getResponse = await _client.GetAsync($"/api/url/{shortCode}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUrl_WithInvalidShortCode_ShouldReturnNotFound()
    {
        // Arrange
        var invalidShortCode = "invalid123";

        // Act
        var response = await _client.DeleteAsync($"/api/url/{invalidShortCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchUrls_WithMatchingTerm_ShouldReturnResults()
    {
        // Arrange
        var url1 = "https://example.com";
        var url2 = "https://test.com";
        var url3 = "https://another-example.org";

        await _client.PostAsJsonAsync("/api/url", new { originalUrl = url1, customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() });
        await _client.PostAsJsonAsync("/api/url", new { originalUrl = url2, customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() });
        await _client.PostAsJsonAsync("/api/url", new { originalUrl = url3, customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() });

        // Act
        var response = await _client.GetAsync("/api/url/search?q=example");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var results = JsonDocument.Parse(content).RootElement.EnumerateArray().ToList();
        
        results.Should().HaveCount(2);
        results.All(r => r.GetProperty("originalUrl").GetString()!.Contains("example")).Should().BeTrue();
    }

    [Fact]
    public async Task CheckAvailability_WithAvailableCode_ShouldReturnTrue()
    {
        // Arrange
        var shortCode = "available123";

        // Act
        var response = await _client.GetAsync($"/api/url/available/{shortCode}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("available").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CheckAvailability_WithTakenCode_ShouldReturnFalse()
    {
        // Arrange
        var customAlias = "taken123";
        var createRequest = new
        {
            originalUrl = "https://example.com",
            customAlias = customAlias,
            expiresAt = "",
            metadata = new Dictionary<string, string>()
        };

        await _client.PostAsJsonAsync("/api/url", createRequest);

        // Act
        var response = await _client.GetAsync($"/api/url/available/{customAlias}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("available").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateMultipleUrls_ShouldGenerateUniqueShortCodes()
    {
        // Arrange
        var requests = new[]
        {
            new { originalUrl = "https://example1.com", customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() },
            new { originalUrl = "https://example2.com", customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() },
            new { originalUrl = "https://example3.com", customAlias = "", expiresAt = "", metadata = new Dictionary<string, string>() }
        };

        var shortCodes = new List<string>();

        // Act
        foreach (var request in requests)
        {
            var response = await _client.PostAsJsonAsync("/api/url", request);
            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);
            var shortCode = jsonDoc.RootElement.GetProperty("shortCode").GetString()!;
            shortCodes.Add(shortCode);
        }

        // Assert
        shortCodes.Should().OnlyHaveUniqueItems();
        shortCodes.Should().AllSatisfy(code => code.Should().NotBeNullOrEmpty());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}