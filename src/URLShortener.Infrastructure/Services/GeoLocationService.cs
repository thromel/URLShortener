using Microsoft.Extensions.Logging;
using System.Net;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Core.Interfaces;

namespace URLShortener.Infrastructure.Services;

public class GeoLocationService : IGeoLocationService
{
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(ILogger<GeoLocationService> logger)
    {
        _logger = logger;
    }

    public async Task<GeoLocation> GetLocationAsync(string ipAddress)
    {
        try
        {
            // Validate IP address
            if (!await IsValidIpAddressAsync(ipAddress))
            {
                return CreateUnknownLocation();
            }

            // For localhost/private IPs, return a default location
            if (IsLocalOrPrivateIP(ipAddress))
            {
                return new GeoLocation("US", "California", "San Francisco", 37.7749, -122.4194);
            }

            // In production, you would integrate with a real geolocation service like:
            // - MaxMind GeoIP2
            // - IP-API
            // - ipstack
            // - freegeoip.app

            // For demo purposes, return mock data based on IP patterns
            return GetMockLocationByIP(ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get geolocation for IP: {IpAddress}", ipAddress);
            return CreateUnknownLocation();
        }
    }

    public async Task<bool> IsValidIpAddressAsync(string ipAddress)
    {
        return await Task.FromResult(IPAddress.TryParse(ipAddress, out _));
    }

    private static bool IsLocalOrPrivateIP(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;

        // Check for loopback
        if (IPAddress.IsLoopback(ip))
            return true;

        // Check for private IP ranges
        var bytes = ip.GetAddressBytes();

        // IPv4 private ranges
        if (bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31))
                return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
        }

        return false;
    }

    private static GeoLocation GetMockLocationByIP(string ipAddress)
    {
        // Simple mock geolocation based on IP address patterns
        // In production, use a real geolocation service

        var hash = ipAddress.GetHashCode();
        var index = Math.Abs(hash) % MockLocations.Length;
        return MockLocations[index];
    }

    private static GeoLocation CreateUnknownLocation()
    {
        return new GeoLocation("Unknown", "Unknown", "Unknown", 0, 0);
    }

    private static readonly GeoLocation[] MockLocations = new[]
    {
        new GeoLocation("US", "California", "San Francisco", 37.7749, -122.4194),
        new GeoLocation("US", "New York", "New York", 40.7128, -74.0060),
        new GeoLocation("US", "Texas", "Austin", 30.2672, -97.7431),
        new GeoLocation("GB", "England", "London", 51.5074, -0.1278),
        new GeoLocation("DE", "Berlin", "Berlin", 52.5200, 13.4050),
        new GeoLocation("FR", "Île-de-France", "Paris", 48.8566, 2.3522),
        new GeoLocation("JP", "Tokyo", "Tokyo", 35.6762, 139.6503),
        new GeoLocation("AU", "New South Wales", "Sydney", -33.8688, 151.2093),
        new GeoLocation("CA", "Ontario", "Toronto", 43.6532, -79.3832),
        new GeoLocation("BR", "São Paulo", "São Paulo", -23.5558, -46.6396),
        new GeoLocation("IN", "Maharashtra", "Mumbai", 19.0760, 72.8777),
        new GeoLocation("SG", "Singapore", "Singapore", 1.3521, 103.8198),
        new GeoLocation("NL", "North Holland", "Amsterdam", 52.3676, 4.9041),
        new GeoLocation("ES", "Madrid", "Madrid", 40.4168, -3.7038),
        new GeoLocation("IT", "Lazio", "Rome", 41.9028, 12.4964),
        new GeoLocation("KR", "Seoul", "Seoul", 37.5665, 126.9780),
        new GeoLocation("RU", "Moscow", "Moscow", 55.7558, 37.6173),
        new GeoLocation("MX", "Mexico City", "Mexico City", 19.4326, -99.1332),
        new GeoLocation("AR", "Buenos Aires", "Buenos Aires", -34.6118, -58.3960),
        new GeoLocation("ZA", "Western Cape", "Cape Town", -33.9249, 18.4241)
    };
}