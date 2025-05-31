using URLShortener.Core.Domain.Enhanced;

namespace URLShortener.Core.Interfaces;

public interface IGeoLocationService
{
    Task<GeoLocation> GetLocationAsync(string ipAddress);
    Task<bool> IsValidIpAddressAsync(string ipAddress);
}