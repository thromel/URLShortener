namespace URLShortener.Core.Interfaces;

public interface ICdnCache
{
    Task InvalidateAsync(string path);
    Task<bool> ExistsAsync(string path);
}