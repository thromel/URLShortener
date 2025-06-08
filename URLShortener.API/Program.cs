using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Services;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Repositories;
using URLShortener.Infrastructure.Services;
using URLShortener.API.Middleware;
using MediatR;
using FluentValidation;
using URLShortener.Core.CQRS.Behaviors;
using Microsoft.FeatureManagement;
using Hangfire;
using Hangfire.SqlServer;
using URLShortener.Infrastructure.BackgroundJobs;
using URLShortener.API.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with enhanced structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "URLShortener")
    .Enrich.WithProperty("Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/urlshortener-.txt", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        retainedFileCountLimit: 31)
    .WriteTo.File("logs/errors-.txt", 
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning,
        retainedFileCountLimit: 31)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

// Add MediatR and CQRS
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(URLShortener.Core.CQRS.Commands.CreateShortUrlCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(URLShortener.Core.CQRS.Validators.CreateShortUrlValidator).Assembly);

// Add Feature Management
builder.Services.AddFeatureManagement();
builder.Services.AddScoped<URLShortener.API.Features.IFeatureFlagService, URLShortener.API.Features.FeatureFlagService>();

// Add Hangfire
var hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(hangfireConnection))
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
    
    builder.Services.AddHangfireServer();
}

// Add response compression
builder.Services.AddResponseCompression();

// Add response caching
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 1024 * 1024; // 1MB
    options.UseCaseSensitivePaths = false;
});

// Add API versioning
builder.Services.AddApiVersioning();

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));
    
    // Specific rate limit for URL creation
    options.AddPolicy("UrlCreation", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2
            }));
    
    // More lenient rate limit for URL redirects
    options.AddPolicy("UrlRedirect", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", cancellationToken);
    };
});

// Database Configuration
builder.Services.AddDbContext<UrlShortenerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        // Fallback to in-memory database for development
        options.UseInMemoryDatabase("URLShortenerDb");
        Log.Warning("Using in-memory database. Configure PostgreSQL connection string for production.");
    }
});

// Memory Cache
builder.Services.AddMemoryCache();

// Redis Cache Configuration
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "URLShortener";
    });
    Log.Information("Redis cache configured with connection: {RedisConnection}", redisConnectionString);
}
else
{
    // Fallback to in-memory distributed cache
    builder.Services.AddDistributedMemoryCache();
    Log.Warning("Using in-memory distributed cache. Configure Redis for production.");
}

// Authentication & Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UrlShortenerDbContext>();

// Register input validation service
builder.Services.AddSingleton<IInputValidationService, InputValidationService>();

// Register metrics service
builder.Services.AddSingleton<IMetrics, MetricsService>();

// Register background jobs
builder.Services.AddScoped<IAnalyticsProcessingJob, AnalyticsProcessingJob>();

// Register domain events
builder.Services.AddScoped<URLShortener.Core.Events.IDomainEventDispatcher, URLShortener.Core.Events.DomainEventDispatcher>();
builder.Services.AddScoped<URLShortener.Core.Events.IDomainEventHandler<URLShortener.Core.Domain.Enhanced.UrlCreatedEvent>, URLShortener.Core.Events.Handlers.UrlCreatedEventHandler>();
builder.Services.AddScoped<URLShortener.Core.Events.IDomainEventHandler<URLShortener.Core.Domain.Enhanced.UrlAccessedEvent>, URLShortener.Core.Events.Handlers.UrlAccessedEventHandler>();
builder.Services.AddScoped<URLShortener.Core.Events.IDomainEventHandler<URLShortener.Core.Domain.Enhanced.UrlDisabledEvent>, URLShortener.Core.Events.Handlers.UrlDisabledEventHandler>();

// Core Service Implementations
var useEnhancedServices = builder.Configuration.GetValue<bool>("Features:UseEnhancedServices", false);

if (useEnhancedServices)
{
    // Enterprise-grade implementations
    builder.Services.AddScoped<IUrlRepository, UrlRepository>();
    builder.Services.AddScoped<ICacheService, HierarchicalCacheService>();
    builder.Services.AddScoped<IEventStore, EventStore>();
    builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
    builder.Services.AddScoped<IGeoLocationService, GeoLocationService>();
    builder.Services.AddScoped<ICdnCache, CloudFrontCacheService>();
    builder.Services.AddScoped<IUrlService, EnhancedUrlService>();

    // Background services
    builder.Services.AddHostedService<PredictiveCacheWarmingService>();

    Log.Information("Using enhanced enterprise services");
}
else
{
    // Basic implementations for development/testing
    builder.Services.AddScoped<IUrlRepository, UrlRepository>();
    builder.Services.AddScoped<IUrlService, BasicUrlService>();
    builder.Services.AddScoped<IAnalyticsService, BasicAnalyticsService>();
    Log.Information("Using basic services for development");
}

// Swagger/OpenAPI with comprehensive documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "URL Shortener API",
        Version = "v1",
        Description = "A comprehensive URL shortener service with analytics, caching, and enterprise features",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "URL Shortener Team",
            Email = "support@urlshortener.com",
            Url = new Uri("https://github.com/urlshortener/api")
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Add security definition
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT Bearer token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add response examples
    options.EnableAnnotations();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://app.urlshortener.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Database Migration (for development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();

    try
    {
        context.Database.EnsureCreated();
        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to initialize database");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "URL Shortener API v1");
        options.RoutePrefix = "docs";
    });
}

// Add correlation ID middleware
app.UseMiddleware<CorrelationIdMiddleware>();

// Add request/response logging
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<RequestResponseLoggingMiddleware>();
}

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseHttpsRedirection();

// Add response compression
app.UseResponseCompression();

// Add response caching
app.UseResponseCaching();

app.UseCors("AllowWebApp");

// Add rate limiting middleware
app.UseRateLimiter();

// Add metrics middleware (simplified)
// app.UseMiddleware<MetricsMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Add Hangfire Dashboard
if (!string.IsNullOrEmpty(hangfireConnection))
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
    
    // Schedule recurring jobs
    RecurringJob.AddOrUpdate<IAnalyticsProcessingJob>(
        "analytics-processing",
        job => job.ProcessAnalyticsBatchAsync(),
        Cron.Hourly);
    
    RecurringJob.AddOrUpdate<IAnalyticsProcessingJob>(
        "daily-reports",
        job => job.GenerateDailyReportsAsync(),
        Cron.Daily(2)); // Run at 2 AM
    
    RecurringJob.AddOrUpdate<IAnalyticsProcessingJob>(
        "cleanup-expired",
        job => job.CleanupExpiredUrlsAsync(),
        Cron.Daily(3)); // Run at 3 AM
    
    RecurringJob.AddOrUpdate<IAnalyticsProcessingJob>(
        "cache-warming",
        job => job.WarmPopularUrlCacheAsync(),
        Cron.MinuteInterval(30)); // Every 30 minutes
}

// Health checks
app.MapHealthChecks("/health");

// API Controllers
app.MapControllers();

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (error != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(error.Error, "Unhandled exception occurred");

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "An internal server error occurred",
                traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier
            }));
        }
    });
});

Log.Information("Starting URL Shortener API with enhanced features: {UseEnhanced}", useEnhancedServices);
Log.Information("Hangfire enabled: {HangfireEnabled}", !string.IsNullOrEmpty(hangfireConnection));
Log.Information("Feature management enabled: {FeatureManagement}", true);

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
