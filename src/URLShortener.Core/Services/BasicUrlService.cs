using Microsoft.Extensions.Logging;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;

namespace URLShortener.Core.Services;

public class BasicUrlService : IUrlService
{
    private readonly ILogger<BasicUrlService> _logger;
    private static readonly Dictionary<string, string> _urlCache = new();

    public BasicUrlService(ILogger<BasicUrlService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateShortUrlAsync(CreateUrlRequest request)
    {
        var shortCode = GenerateShortCode();
        _urlCache[shortCode] = request.OriginalUrl;

        _logger.LogInformation("Created short URL {ShortCode} for {OriginalUrl}", shortCode, request.OriginalUrl);

        return await Task.FromResult(shortCode);
    }

    public async Task<string?> GetOriginalUrlAsync(string shortCode)
    {
        _urlCache.TryGetValue(shortCode, out var url);
        return await Task.FromResult(url);
    }

    public async Task<UrlStatistics> GetUrlStatisticsAsync(string shortCode)
    {
        var originalUrl = await GetOriginalUrlAsync(shortCode);
        if (string.IsNullOrEmpty(originalUrl))
        {
            throw new ArgumentException($"Short code '{shortCode}' not found");
        }

        return new UrlStatistics(
            ShortCode: shortCode,
            OriginalUrl: originalUrl,
            AccessCount: 0,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            LastAccessedAt: null,
            ExpiresAt: null,
            Status: UrlStatus.Active,
            CountryStats: new Dictionary<string, long>(),
            DeviceStats: new Dictionary<string, long>(),
            ReferrerStats: new Dictionary<string, long>()
        );
    }

    public async Task<bool> DeleteUrlAsync(string shortCode)
    {
        var removed = _urlCache.Remove(shortCode);
        _logger.LogInformation("Deleted URL {ShortCode}: {Success}", shortCode, removed);
        return await Task.FromResult(removed);
    }

    public async Task<IEnumerable<UrlStatistics>> GetUserUrlsAsync(Guid userId, int skip = 0, int take = 50)
    {
        // Basic implementation - return empty list
        return await Task.FromResult(new List<UrlStatistics>());
    }

    public async Task<bool> IsAvailableAsync(string shortCode)
    {
        return await Task.FromResult(!_urlCache.ContainsKey(shortCode));
    }

    public async Task RecordAccessAsync(string shortCode, string ipAddress, string userAgent, string referrer = "")
    {
        _logger.LogDebug("Recorded access for {ShortCode} from {IpAddress}", shortCode, ipAddress);
        await Task.CompletedTask;
    }

    public async Task DisableUrlAsync(string shortCode, DisableReason reason, string? adminNotes = null)
    {
        // For basic implementation, we'll just delete it
        await DeleteUrlAsync(shortCode);
        _logger.LogInformation("Disabled URL {ShortCode} for reason: {Reason}", shortCode, reason);
    }

    public async Task<IEnumerable<UrlStatistics>> SearchUrlsAsync(string searchTerm, int skip = 0, int take = 50)
    {
        var matchingUrls = _urlCache
            .Where(kvp => kvp.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Skip(skip)
            .Take(take)
            .Select(kvp => new UrlStatistics(
                ShortCode: kvp.Key,
                OriginalUrl: kvp.Value,
                AccessCount: 0,
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                LastAccessedAt: null,
                ExpiresAt: null,
                Status: UrlStatus.Active,
                CountryStats: new Dictionary<string, long>(),
                DeviceStats: new Dictionary<string, long>(),
                ReferrerStats: new Dictionary<string, long>()
            ));

        return await Task.FromResult(matchingUrls);
    }

    private static string GenerateShortCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}