using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Services;
using URLShortener.Core.Telemetry;
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
using Hangfire.InMemory;
using URLShortener.Infrastructure.BackgroundJobs;
using URLShortener.API.Authorization;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
builder.Services.AddHttpContextAccessor();

// Add SignalR for real-time notifications
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Add GraphQL with Hot Chocolate
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<URLShortener.API.GraphQL.Query>()
    .AddMutationType<URLShortener.API.GraphQL.Mutation>()
    .AddSubscriptionType<URLShortener.API.GraphQL.Subscription>()
    .AddInMemorySubscriptions()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .ModifyRequestOptions(opt =>
    {
        opt.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

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

// Add OpenTelemetry distributed tracing
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: Diagnostics.ServiceName,
            serviceVersion: Diagnostics.ServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["host.name"] = Environment.MachineName
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(Diagnostics.ServiceName)
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                {
                    // Don't trace health checks or static files
                    var path = httpContext.Request.Path.Value ?? "";
                    return !path.StartsWith("/health") && !path.StartsWith("/docs");
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
            });

        // Add OTLP exporter if endpoint is configured
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            });
            Log.Information("OpenTelemetry OTLP exporter configured: {Endpoint}", otlpEndpoint);
        }
        else
        {
            // Use console exporter for development
            tracing.AddConsoleExporter();
            Log.Information("OpenTelemetry console exporter enabled (no OTLP endpoint configured)");
        }
    });

// Add Hangfire
var hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemoryHangfire = builder.Environment.IsDevelopment() || string.IsNullOrEmpty(hangfireConnection);

if (useInMemoryHangfire)
{
    // Use in-memory storage for development
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseInMemoryStorage());

    builder.Services.AddHangfireServer();
    Log.Information("Hangfire configured with in-memory storage for development");
}
else if (!string.IsNullOrEmpty(hangfireConnection))
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
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"),
        new Asp.Versioning.QueryStringApiVersionReader("api-version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

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
        Log.Information("Using PostgreSQL database");
    }
    else
    {
        // Fallback to in-memory database if no connection string
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

// Authentication & Authorization - Self-contained JWT validation
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "URLShortener";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "urlshortener-api";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.FromMinutes(1) // Allow 1 minute clock skew
    };

    // Configure events for debugging and SignalR support
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Allow JWT token in query string for SignalR
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException))
            {
                context.Response.Headers["Token-Expired"] = "true";
            }
            Log.Warning("Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Register Auth Service
builder.Services.AddScoped<IAuthService, AuthService>();

// Register Organization Service
builder.Services.AddScoped<IOrganizationService, OrganizationService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UrlShortenerDbContext>();

// Register input validation service
builder.Services.AddSingleton<IInputValidationService, InputValidationService>();

// Register metrics service
builder.Services.AddSingleton<IMetrics, MetricsService>();

// Register utility services
builder.Services.AddScoped<URLShortener.API.Services.IQRCodeService, URLShortener.API.Services.QRCodeService>();
builder.Services.AddScoped<URLShortener.API.Services.IClientInfoService, URLShortener.API.Services.ClientInfoService>();
builder.Services.AddScoped<URLShortener.API.Services.IAnalyticsExportService, URLShortener.API.Services.AnalyticsExportService>();

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
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    // Access pattern analysis for predictive caching
    builder.Services.AddScoped<IAccessPatternAnalyzer, AccessPatternAnalyzer>();

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
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    // Required by MediatR handlers even in basic mode
    builder.Services.AddScoped<ICacheService, HierarchicalCacheService>();
    builder.Services.AddScoped<IGeoLocationService, GeoLocationService>();
    builder.Services.AddScoped<IEventStore, EventStore>();
    builder.Services.AddScoped<ICdnCache, CloudFrontCacheService>();
    Log.Information("Using basic services for development");
}

// Swagger/OpenAPI with comprehensive documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add API versions
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
    
    options.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "URL Shortener API",
        Version = "v2",
        Description = "Enhanced URL shortener API with bulk operations, QR codes, and advanced analytics",
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
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
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
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:4200",  // Angular dev server
                "https://app.urlshortener.com")
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
        options.SwaggerEndpoint("/swagger/v2/swagger.json", "URL Shortener API v2");
        options.RoutePrefix = "docs";
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        options.DisplayRequestDuration();
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

// Add Hangfire Dashboard (always available now - uses in-memory in development)
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
    "*/30 * * * *"); // Every 30 minutes

// Health checks
app.MapHealthChecks("/health");

// API Controllers
app.MapControllers();

// SignalR Hubs
app.MapHub<URLShortener.API.Hubs.AnalyticsHub>("/hubs/analytics");

// GraphQL endpoint
app.MapGraphQL("/graphql");
app.UseWebSockets(); // Required for GraphQL subscriptions

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
Log.Information("SignalR real-time notifications enabled at /hubs/analytics");
Log.Information("GraphQL API enabled at /graphql");

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
