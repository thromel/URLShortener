using System.Diagnostics;

namespace URLShortener.Core.Telemetry;

/// <summary>
/// Centralized diagnostic instrumentation for distributed tracing.
/// Uses System.Diagnostics.ActivitySource for OpenTelemetry-compatible tracing.
/// </summary>
public static class Diagnostics
{
    /// <summary>
    /// The service name used for trace identification.
    /// </summary>
    public const string ServiceName = "URLShortener.Core";

    /// <summary>
    /// The current service version.
    /// </summary>
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// The shared ActivitySource for creating spans across all handlers and services.
    /// OpenTelemetry will listen to this source to collect trace data.
    /// </summary>
    public static readonly ActivitySource Source = new(ServiceName, ServiceVersion);

    /// <summary>
    /// Common tag keys for consistent span attribution.
    /// </summary>
    public static class Tags
    {
        public const string ShortCode = "url.short_code";
        public const string OriginalUrl = "url.original";
        public const string UserId = "user.id";
        public const string OperationType = "operation.type";
        public const string CacheHit = "cache.hit";
        public const string CacheLevel = "cache.level";
        public const string ErrorType = "error.type";
        public const string UrlStatus = "url.status";
        public const string ResultCount = "result.count";
    }

    /// <summary>
    /// Standard operation names for consistent span naming.
    /// </summary>
    public static class Operations
    {
        public const string CreateShortUrl = "CreateShortUrl";
        public const string DeleteUrl = "DeleteUrl";
        public const string GetUrl = "GetUrl";
        public const string GetOriginalUrl = "GetOriginalUrl";
        public const string GetUrlStatistics = "GetUrlStatistics";
        public const string CheckUrlAvailability = "CheckUrlAvailability";
        public const string GetUserUrls = "GetUserUrls";
        public const string SearchUrls = "SearchUrls";
        public const string CacheLookup = "CacheLookup";
        public const string DatabaseQuery = "DatabaseQuery";
        public const string RecordAnalytics = "RecordAnalytics";
    }

    /// <summary>
    /// Starts an activity with the given name and optional parent context.
    /// </summary>
    /// <param name="operationName">The name of the operation being traced.</param>
    /// <param name="kind">The kind of activity (Internal, Server, Client, Producer, Consumer).</param>
    /// <returns>A new Activity if sampling is enabled, null otherwise.</returns>
    public static Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return Source.StartActivity(operationName, kind);
    }

    /// <summary>
    /// Adds standard error information to an activity.
    /// </summary>
    /// <param name="activity">The activity to add error info to.</param>
    /// <param name="exception">The exception that occurred.</param>
    public static void RecordException(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag(Tags.ErrorType, exception.GetType().Name);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", exception.GetType().FullName },
            { "exception.message", exception.Message },
            { "exception.stacktrace", exception.StackTrace }
        }));
    }

    /// <summary>
    /// Marks an activity as successfully completed.
    /// </summary>
    /// <param name="activity">The activity to mark as successful.</param>
    public static void RecordSuccess(Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
