using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using URLShortener.Infrastructure.Data;

namespace URLShortener.Tests.Integration;

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<UrlShortenerDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database for testing
            services.AddDbContext<UrlShortenerDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });

            // Replace Redis with in-memory cache for testing
            var distributedCacheDescriptor = services.SingleOrDefault(
                d => d.ServiceType.Name.Contains("IDistributedCache"));
            if (distributedCacheDescriptor != null)
                services.Remove(distributedCacheDescriptor);
            
            services.AddDistributedMemoryCache();

            // Disable authentication for testing
            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultScheme = "Test";
            });

            // Override logging
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        });

        builder.UseEnvironment("Testing");
    }
}