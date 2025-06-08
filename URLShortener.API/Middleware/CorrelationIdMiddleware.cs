using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace URLShortener.API.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeaderName, correlationId);
            return Task.CompletedTask;
        });

        // Add to logging context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Add to HttpContext for access in controllers
            context.Items["CorrelationId"] = correlationId;
            
            _logger.LogDebug("Processing request with correlation ID: {CorrelationId}", correlationId);
            
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out StringValues correlationId) &&
            !StringValues.IsNullOrEmpty(correlationId))
        {
            return correlationId.FirstOrDefault() ?? GenerateCorrelationId();
        }

        return GenerateCorrelationId();
    }

    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..12]; // Shorter correlation ID
    }
}

public static class CorrelationIdExtensions
{
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue("CorrelationId", out var correlationId) 
            ? correlationId?.ToString() 
            : null;
    }
}