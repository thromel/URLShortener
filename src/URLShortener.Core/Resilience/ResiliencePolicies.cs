using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace URLShortener.Core.Resilience;

public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetHttpClientPolicy(ILogger logger)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} after {Delay}ms for {OperationKey}",
                        retryCount, timespan.TotalMilliseconds, context.OperationKey);
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    logger.LogWarning("Circuit breaker opened for {Duration}s due to {Exception}",
                        duration.TotalSeconds, exception.GetType().Name);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset");
                });

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 second timeout

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }

    public static IAsyncPolicy GetDatabasePolicy(ILogger logger)
    {
        return Policy
            .Handle<Exception>(ex => IsDatabaseTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger.LogWarning(exception, "Database retry {RetryCount} after {Delay}ms for {OperationKey}",
                        retryCount, timespan.TotalMilliseconds, context.OperationKey);
                });
    }

    public static IAsyncPolicy GetCachePolicy(ILogger logger)
    {
        return Policy
            .Handle<Exception>(ex => IsCacheTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(50 * retryAttempt),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    logger.LogWarning(exception, "Cache retry {RetryCount} after {Delay}ms for {OperationKey}",
                        retryCount, timespan.TotalMilliseconds, context.OperationKey);
                });
    }

    private static bool IsDatabaseTransientError(Exception ex)
    {
        // Add specific database transient error detection logic
        return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCacheTransientError(Exception ex)
    {
        // Add specific cache transient error detection logic
        return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("redis", StringComparison.OrdinalIgnoreCase);
    }
}