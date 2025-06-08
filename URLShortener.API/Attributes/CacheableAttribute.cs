using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace URLShortener.API.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class CacheableAttribute : Attribute, IActionFilter
{
    private readonly int _duration;
    private readonly string? _cacheKeyPrefix;

    public CacheableAttribute(int durationSeconds = 300, string? cacheKeyPrefix = null)
    {
        _duration = durationSeconds;
        _cacheKeyPrefix = cacheKeyPrefix;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var cacheKey = GenerateCacheKey(context);
        var etag = context.HttpContext.Request.Headers.IfNoneMatch.FirstOrDefault();

        if (!string.IsNullOrEmpty(etag))
        {
            var cachedETag = context.HttpContext.RequestServices
                .GetService<IMemoryCache>()?.Get<string>($"{cacheKey}:etag");

            if (etag.Trim('"') == cachedETag)
            {
                context.Result = new StatusCodeResult(304); // Not Modified
                return;
            }
        }

        context.HttpContext.Items["CacheKey"] = cacheKey;
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is not ObjectResult objectResult || objectResult.Value == null)
            return;

        var cacheKey = context.HttpContext.Items["CacheKey"]?.ToString();
        if (string.IsNullOrEmpty(cacheKey))
            return;

        var memoryCache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
        if (memoryCache == null)
            return;

        // Generate ETag based on content
        var content = JsonSerializer.Serialize(objectResult.Value);
        var etag = GenerateETag(content);

        // Set cache headers
        context.HttpContext.Response.Headers.ETag = $"\"{etag}\"";
        context.HttpContext.Response.Headers.CacheControl = $"public, max-age={_duration}";
        context.HttpContext.Response.Headers.LastModified = DateTime.UtcNow.ToString("R");

        // Cache the ETag
        memoryCache.Set($"{cacheKey}:etag", etag, TimeSpan.FromSeconds(_duration));
        memoryCache.Set(cacheKey, objectResult.Value, TimeSpan.FromSeconds(_duration));
    }

    private string GenerateCacheKey(ActionExecutingContext context)
    {
        var keyBuilder = new StringBuilder();
        
        if (!string.IsNullOrEmpty(_cacheKeyPrefix))
        {
            keyBuilder.Append(_cacheKeyPrefix).Append(':');
        }

        keyBuilder.Append(context.ActionDescriptor.DisplayName);

        // Add route values
        foreach (var (key, value) in context.RouteData.Values)
        {
            keyBuilder.Append(':').Append(key).Append('=').Append(value);
        }

        // Add query parameters
        foreach (var (key, value) in context.HttpContext.Request.Query)
        {
            keyBuilder.Append(':').Append(key).Append('=').Append(value);
        }

        return keyBuilder.ToString();
    }

    private static string GenerateETag(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash)[..16]; // Shortened ETag
    }
}