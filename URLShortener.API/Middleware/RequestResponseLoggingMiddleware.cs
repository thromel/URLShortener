using Serilog;
using Serilog.Context;
using System.Diagnostics;
using System.Text;

namespace URLShortener.API.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];

        // Log request
        await LogRequestAsync(context, requestId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Log response
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds, responseBody);
            
            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("RequestMethod", request.Method))
        using (LogContext.PushProperty("RequestPath", request.Path))
        using (LogContext.PushProperty("RequestQuery", request.QueryString.ToString()))
        using (LogContext.PushProperty("UserAgent", request.Headers.UserAgent.ToString()))
        using (LogContext.PushProperty("RemoteIpAddress", GetClientIpAddress(context)))
        {
            var body = await ReadRequestBodyAsync(request);
            
            _logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath}{RequestQuery} - Request ID: {RequestId}",
                request.Method,
                request.Path,
                request.QueryString,
                requestId);

            if (!string.IsNullOrEmpty(body) && ShouldLogRequestBody(request))
            {
                _logger.LogDebug("Request body: {RequestBody}", body);
            }
        }
    }

    private async Task LogResponseAsync(HttpContext context, string requestId, long elapsedMs, MemoryStream responseBody)
    {
        var response = context.Response;
        
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("ResponseStatusCode", response.StatusCode))
        using (LogContext.PushProperty("ResponseTime", elapsedMs))
        {
            var responseBodyText = await ReadResponseBodyAsync(responseBody);
            
            var logLevel = response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
            
            _logger.Log(logLevel,
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMs}ms - Request ID: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                response.StatusCode,
                elapsedMs,
                requestId);

            if (!string.IsNullOrEmpty(responseBodyText) && ShouldLogResponseBody(response))
            {
                _logger.LogDebug("Response body: {ResponseBody}", responseBodyText);
            }
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            request.EnableBuffering();
        }

        request.Body.Position = 0;
        var body = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(MemoryStream responseBody)
    {
        responseBody.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(responseBody).ReadToEndAsync();
        responseBody.Seek(0, SeekOrigin.Begin);

        return body;
    }

    private static bool ShouldLogRequestBody(HttpRequest request)
    {
        return request.ContentType?.Contains("application/json") == true ||
               request.ContentType?.Contains("application/xml") == true ||
               request.ContentType?.Contains("text/") == true;
    }

    private static bool ShouldLogResponseBody(HttpResponse response)
    {
        return response.ContentType?.Contains("application/json") == true ||
               response.ContentType?.Contains("application/xml") == true ||
               response.ContentType?.Contains("text/") == true;
    }

    private static string GetClientIpAddress(HttpContext context)
    {
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
}