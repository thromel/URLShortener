using System.Security.Claims;

namespace URLShortener.API.Services;

/// <summary>
/// Service for extracting client information from HTTP context
/// </summary>
public interface IClientInfoService
{
    /// <summary>
    /// Gets the current user ID from claims or returns default
    /// </summary>
    /// <returns>User ID</returns>
    Guid GetUserId();

    /// <summary>
    /// Gets the client IP address considering proxy headers
    /// </summary>
    /// <returns>Client IP address</returns>
    string GetClientIpAddress();

    /// <summary>
    /// Gets the user agent string
    /// </summary>
    /// <returns>User agent</returns>
    string GetUserAgent();

    /// <summary>
    /// Gets the referrer URL
    /// </summary>
    /// <returns>Referrer URL</returns>
    string GetReferrer();
}

public class ClientInfoService : IClientInfoService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ClientInfoService> _logger;

    public ClientInfoService(IHttpContextAccessor httpContextAccessor, ILogger<ClientInfoService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid GetUserId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
        }

        // For demo purposes, return a default user ID
        // In production, this should be properly authenticated
        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public string GetClientIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return "unknown";

        // Try to get the real IP address from headers (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public string GetUserAgent()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.Request.Headers.UserAgent.ToString() ?? string.Empty;
    }

    public string GetReferrer()
    {
        var context = _httpContextAccessor.HttpContext;
        return context?.Request.Headers.Referer.ToString() ?? string.Empty;
    }
}