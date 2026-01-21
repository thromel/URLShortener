using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using URLShortener.Core.Services;

namespace URLShortener.API.Authorization;

/// <summary>
/// Attribute to require a specific permission for an action.
/// The organization ID must be provided via route parameter named "id" or "organizationId".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;
    private readonly string _organizationIdParam;

    public RequirePermissionAttribute(string permission, string organizationIdParam = "id")
    {
        _permission = permission;
        _organizationIdParam = organizationIdParam;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get user ID from claims
        var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get organization ID from route
        if (!context.RouteData.Values.TryGetValue(_organizationIdParam, out var orgIdValue) ||
            !Guid.TryParse(orgIdValue?.ToString(), out var organizationId))
        {
            // Try query string
            var queryOrgId = context.HttpContext.Request.Query[_organizationIdParam].FirstOrDefault();
            if (queryOrgId == null || !Guid.TryParse(queryOrgId, out organizationId))
            {
                context.Result = new BadRequestObjectResult(new { error = $"Organization ID not found in route parameter '{_organizationIdParam}'" });
                return;
            }
        }

        // Get the organization service
        var organizationService = context.HttpContext.RequestServices.GetService<IOrganizationService>();
        if (organizationService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Check permission
        var hasPermission = await organizationService.HasPermissionAsync(userId, organizationId, _permission);

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}

/// <summary>
/// Attribute to require the user to be a member of the organization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireOrganizationMembershipAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _organizationIdParam;

    public RequireOrganizationMembershipAttribute(string organizationIdParam = "id")
    {
        _organizationIdParam = organizationIdParam;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.RouteData.Values.TryGetValue(_organizationIdParam, out var orgIdValue) ||
            !Guid.TryParse(orgIdValue?.ToString(), out var organizationId))
        {
            context.Result = new BadRequestObjectResult(new { error = $"Organization ID not found in route parameter '{_organizationIdParam}'" });
            return;
        }

        var organizationService = context.HttpContext.RequestServices.GetService<IOrganizationService>();
        if (organizationService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        var isMember = await organizationService.IsMemberAsync(userId, organizationId);

        if (!isMember)
        {
            context.Result = new ForbidResult();
        }
    }
}

/// <summary>
/// Attribute to require the user to be the owner of the organization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireOrganizationOwnerAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _organizationIdParam;

    public RequireOrganizationOwnerAttribute(string organizationIdParam = "id")
    {
        _organizationIdParam = organizationIdParam;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.RouteData.Values.TryGetValue(_organizationIdParam, out var orgIdValue) ||
            !Guid.TryParse(orgIdValue?.ToString(), out var organizationId))
        {
            context.Result = new BadRequestObjectResult(new { error = $"Organization ID not found in route parameter '{_organizationIdParam}'" });
            return;
        }

        var organizationService = context.HttpContext.RequestServices.GetService<IOrganizationService>();
        if (organizationService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        var isOwner = await organizationService.IsOwnerAsync(userId, organizationId);

        if (!isOwner)
        {
            context.Result = new ForbidResult();
        }
    }
}
