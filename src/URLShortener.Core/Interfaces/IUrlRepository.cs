using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public interface IUrlRepository
{
    Task<string?> GetOriginalUrlAsync(string shortCode);
    Task<ShortUrlAggregate?> GetByIdAsync(Guid id);
    Task<ShortUrlAggregate?> GetByShortCodeAsync(string shortCode);
    Task SaveAsync(ShortUrlAggregate aggregate);
    Task DeleteAsync(string shortCode);
    Task<IEnumerable<ShortUrlAggregate>> GetByUserIdAsync(Guid userId, int skip = 0, int take = 50);
    Task<bool> ExistsAsync(string shortCode);
    Task<long> GetTotalCountAsync();
    Task<IEnumerable<ShortUrlAggregate>> GetExpiredUrlsAsync(DateTime cutoffDate);
    Task<IEnumerable<ShortUrlAggregate>> SearchAsync(string searchTerm, int skip = 0, int take = 50);
}