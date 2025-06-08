using Hangfire.Dashboard;

namespace URLShortener.API.Authorization;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // In development, allow all access
        if (httpContext.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true)
        {
            return true;
        }

        // In production, implement proper authorization
        // This is a simplified example - in real scenarios, you'd check:
        // - User authentication
        // - User roles/permissions
        // - IP whitelist
        // - etc.
        
        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
    }
}