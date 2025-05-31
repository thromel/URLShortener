using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public record CreateUrlRequest(
    string OriginalUrl,
    Guid UserId,
    string? CustomAlias = null,
    DateTime? ExpiresAt = null,
    Dictionary<string, string>? Metadata = null,
    string IpAddress = "",
    string UserAgent = ""
);

public record UrlStatistics(
    string ShortCode,
    string OriginalUrl,
    long AccessCount,
    DateTime CreatedAt,
    DateTime? LastAccessedAt,
    DateTime? ExpiresAt,
    UrlStatus Status,
    Dictionary<string, long> CountryStats,
    Dictionary<string, long> DeviceStats,
    Dictionary<string, long> ReferrerStats
);

public interface IUrlService
{
    Task<string> CreateShortUrlAsync(CreateUrlRequest request);
    Task<string?> GetOriginalUrlAsync(string shortCode);
    Task<UrlStatistics> GetUrlStatisticsAsync(string shortCode);
    Task<bool> DeleteUrlAsync(string shortCode);
    Task<IEnumerable<UrlStatistics>> GetUserUrlsAsync(Guid userId, int skip = 0, int take = 50);
    Task<bool> IsAvailableAsync(string shortCode);
    Task RecordAccessAsync(string shortCode, string ipAddress, string userAgent, string referrer = "");
    Task DisableUrlAsync(string shortCode, DisableReason reason, string? adminNotes = null);
    Task<IEnumerable<UrlStatistics>> SearchUrlsAsync(string searchTerm, int skip = 0, int take = 50);
}
