using System.Diagnostics;
using URLShortener.Infrastructure.Services;

namespace URLShortener.API.Middleware;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMetrics _metrics;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, IMetrics metrics, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            var endpoint = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown";
            var method = context.Request.Method;
            var statusCode = context.Response.StatusCode;
            var duration = stopwatch.Elapsed.TotalMilliseconds;

            _metrics.RecordApiCall(endpoint, method, statusCode, duration);

            if (statusCode >= 500)
            {
                _metrics.RecordError("server_error");
            }
            else if (statusCode >= 400)
            {
                _metrics.RecordError("client_error");
            }

            _logger.LogDebug(
                "Request {Method} {Path} responded {StatusCode} in {Duration}ms",
                method, context.Request.Path, statusCode, duration);
        }
    }
}