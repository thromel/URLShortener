using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public enum CacheInvalidationReason
{
    ManualInvalidation,
    UrlDeleted,
    UrlExpired,
    SuspiciousActivity,
    PolicyViolation,
    SystemMaintenance
}

public interface ICacheService
{
    Task<string?> GetOriginalUrlAsync(string shortCode);
    Task SetAsync(string shortCode, string originalUrl, TimeSpan? expiry = null);
    Task InvalidateAsync(string shortCode, CacheInvalidationReason reason);
    Task<bool> ExistsAsync(string shortCode);
    Task InvalidatePatternAsync(string pattern);
}