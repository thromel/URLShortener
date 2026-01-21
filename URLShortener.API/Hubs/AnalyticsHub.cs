using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace URLShortener.API.Hubs;

/// <summary>
/// SignalR hub for real-time analytics notifications.
/// Clients can subscribe to specific URLs or their entire user feed.
/// </summary>
[Authorize]
public class AnalyticsHub : Hub<IAnalyticsClient>
{
    private readonly ILogger<AnalyticsHub> _logger;

    public AnalyticsHub(ILogger<AnalyticsHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? "anonymous";
        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}",
            Context.ConnectionId, userId);

        // Automatically join user's personal group for their URL updates
        if (!string.IsNullOrEmpty(userId) && userId != "anonymous")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogDebug("Added {ConnectionId} to user group: user:{UserId}",
                Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.Name ?? "anonymous";

        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}, User: {UserId}",
                Context.ConnectionId, userId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}",
                Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to real-time updates for a specific short URL.
    /// </summary>
    /// <param name="shortCode">The short code to subscribe to.</param>
    public async Task SubscribeToUrl(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new HubException("Short code cannot be empty");
        }

        var groupName = $"url:{shortCode}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug("Client {ConnectionId} subscribed to URL: {ShortCode}",
            Context.ConnectionId, shortCode);

        // Send confirmation
        await Clients.Caller.ReceiveSystemNotification(
            $"Subscribed to updates for {shortCode}", "info");
    }

    /// <summary>
    /// Unsubscribe from updates for a specific short URL.
    /// </summary>
    /// <param name="shortCode">The short code to unsubscribe from.</param>
    public async Task UnsubscribeFromUrl(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new HubException("Short code cannot be empty");
        }

        var groupName = $"url:{shortCode}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug("Client {ConnectionId} unsubscribed from URL: {ShortCode}",
            Context.ConnectionId, shortCode);
    }

    /// <summary>
    /// Subscribe to all URL updates for a specific user.
    /// Useful for dashboard views.
    /// </summary>
    /// <param name="userId">The user ID to subscribe to (must match authenticated user).</param>
    public async Task SubscribeToUserFeed(string userId)
    {
        var authenticatedUserId = Context.User?.Identity?.Name;

        // Only allow subscribing to own feed
        if (authenticatedUserId != userId)
        {
            throw new HubException("Cannot subscribe to another user's feed");
        }

        var groupName = $"user:{userId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug("Client {ConnectionId} subscribed to user feed: {UserId}",
            Context.ConnectionId, userId);
    }

    /// <summary>
    /// Subscribe to global analytics (admin only).
    /// Receives all URL access events across the system.
    /// </summary>
    public async Task SubscribeToGlobalFeed()
    {
        // TODO: Add admin role check
        var isAdmin = Context.User?.IsInRole("Admin") ?? false;

        if (!isAdmin)
        {
            throw new HubException("Only administrators can subscribe to global feed");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "global");

        _logger.LogInformation("Admin client {ConnectionId} subscribed to global feed",
            Context.ConnectionId);
    }

    /// <summary>
    /// Request current statistics for a URL.
    /// Useful for initial load when connecting.
    /// </summary>
    /// <param name="shortCode">The short code to get statistics for.</param>
    public async Task RequestStatistics(string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            throw new HubException("Short code cannot be empty");
        }

        _logger.LogDebug("Statistics requested for {ShortCode} by {ConnectionId}",
            shortCode, Context.ConnectionId);

        // Note: Actual statistics retrieval would be handled by injecting IMediator
        // and sending a GetUrlStatisticsQuery. For now, this is a placeholder
        // that the domain event handler will populate.
    }
}

/// <summary>
/// Extension methods for broadcasting analytics events via SignalR.
/// </summary>
public static class AnalyticsHubExtensions
{
    /// <summary>
    /// Broadcasts an access event to all clients subscribed to the URL.
    /// </summary>
    public static async Task BroadcastAccessEvent(
        this IHubContext<AnalyticsHub, IAnalyticsClient> hubContext,
        string shortCode,
        string? userId,
        AccessEventDto accessEvent)
    {
        // Send to URL-specific subscribers
        await hubContext.Clients.Group($"url:{shortCode}")
            .ReceiveAccessEvent(shortCode, accessEvent);

        // Send to user's personal feed if they own this URL
        if (!string.IsNullOrEmpty(userId))
        {
            await hubContext.Clients.Group($"user:{userId}")
                .ReceiveAccessEvent(shortCode, accessEvent);
        }

        // Send to global feed (admin dashboard)
        await hubContext.Clients.Group("global")
            .ReceiveAccessEvent(shortCode, accessEvent);
    }

    /// <summary>
    /// Broadcasts statistics update to all clients subscribed to the URL.
    /// </summary>
    public static async Task BroadcastStatisticsUpdate(
        this IHubContext<AnalyticsHub, IAnalyticsClient> hubContext,
        string shortCode,
        string? userId,
        StatisticsUpdateDto stats)
    {
        await hubContext.Clients.Group($"url:{shortCode}")
            .ReceiveStatisticsUpdate(shortCode, stats);

        if (!string.IsNullOrEmpty(userId))
        {
            await hubContext.Clients.Group($"user:{userId}")
                .ReceiveStatisticsUpdate(shortCode, stats);
        }
    }

    /// <summary>
    /// Broadcasts URL creation to the user's personal feed.
    /// </summary>
    public static async Task BroadcastUrlCreated(
        this IHubContext<AnalyticsHub, IAnalyticsClient> hubContext,
        string userId,
        UrlCreatedDto urlCreated)
    {
        await hubContext.Clients.Group($"user:{userId}")
            .ReceiveUrlCreated(urlCreated);
    }

    /// <summary>
    /// Broadcasts URL deletion notification.
    /// </summary>
    public static async Task BroadcastUrlDeleted(
        this IHubContext<AnalyticsHub, IAnalyticsClient> hubContext,
        string shortCode,
        string? userId)
    {
        await hubContext.Clients.Group($"url:{shortCode}")
            .ReceiveUrlDeleted(shortCode);

        if (!string.IsNullOrEmpty(userId))
        {
            await hubContext.Clients.Group($"user:{userId}")
                .ReceiveUrlDeleted(shortCode);
        }
    }
}
