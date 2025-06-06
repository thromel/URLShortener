using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;
using URLShortener.Core.Interfaces;
using URLShortener.Core.Services;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Repositories;
using URLShortener.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/urlshortener-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

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

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "URL Shortener API",
        Version = "v1",
        Description = "Enterprise-grade URL shortener with advanced analytics and caching",
        Contact = new OpenApiContact
        {
            Name = "URL Shortener Team",
            Email = "support@urlshortener.com"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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
app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

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
