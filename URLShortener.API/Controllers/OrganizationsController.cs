using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using URLShortener.API.DTOs.Organizations;
using URLShortener.API.Services;
using URLShortener.Core.Services;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.API.Controllers;

/// <summary>
/// Controller for managing organizations and their members
/// </summary>
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public class OrganizationsController : BaseApiController
{
    private readonly IOrganizationService _organizationService;
    private readonly IAuthService _authService;

    public OrganizationsController(
        IOrganizationService organizationService,
        IAuthService authService,
        IClientInfoService clientInfoService,
        ILogger<OrganizationsController> logger)
        : base(clientInfoService, logger)
    {
        _organizationService = organizationService;
        _authService = authService;
    }

    /// <summary>
    /// Get all organizations the current user belongs to
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Get user's organizations", Description = "Returns all organizations the current user is a member of")]
    [SwaggerResponse(200, "List of organizations", typeof(List<OrganizationDto>))]
    public async Task<IActionResult> GetMyOrganizations(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var organizations = await _organizationService.GetUserOrganizationsAsync(userId, cancellationToken);

        return Ok(organizations.Select(o => new OrganizationDto(
            o.Id, o.Name, o.Slug, o.OwnerId, o.OwnerName,
            o.CreatedAt, o.IsActive, o.MemberCount, o.UrlCount
        )));
    }

    /// <summary>
    /// Create a new organization
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create organization", Description = "Creates a new organization with the current user as owner")]
    [SwaggerResponse(201, "Organization created", typeof(OrganizationDetailDto))]
    [SwaggerResponse(400, "Invalid request or slug already exists")]
    public async Task<IActionResult> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        var result = await _organizationService.CreateOrganizationAsync(
            userId, request.Name, request.Slug, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        Logger.LogInformation("Organization created: {OrgId} by user {UserId}", result.Organization!.Id, userId);

        return CreatedAtAction(
            nameof(GetOrganization),
            new { id = result.Organization.Id },
            MapToDetailDto(result.Organization));
    }

    /// <summary>
    /// Get organization by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get organization", Description = "Returns organization details if user is a member")]
    [SwaggerResponse(200, "Organization details", typeof(OrganizationDetailDto))]
    [SwaggerResponse(403, "User is not a member")]
    [SwaggerResponse(404, "Organization not found")]
    public async Task<IActionResult> GetOrganization(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.IsMemberAsync(userId, id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _organizationService.GetOrganizationAsync(id, cancellationToken);

        if (!result.Success)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(MapToDetailDto(result.Organization!));
    }

    /// <summary>
    /// Get organization by slug
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    [SwaggerOperation(Summary = "Get organization by slug", Description = "Returns organization details by slug if user is a member")]
    [SwaggerResponse(200, "Organization details", typeof(OrganizationDetailDto))]
    [SwaggerResponse(403, "User is not a member")]
    [SwaggerResponse(404, "Organization not found")]
    public async Task<IActionResult> GetOrganizationBySlug(string slug, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _organizationService.GetOrganizationBySlugAsync(slug, cancellationToken);

        if (!result.Success)
        {
            return NotFound(new { error = result.Error });
        }

        if (!await _organizationService.IsMemberAsync(userId, result.Organization!.Id, cancellationToken))
        {
            return Forbid();
        }

        return Ok(MapToDetailDto(result.Organization));
    }

    /// <summary>
    /// Update organization settings
    /// </summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Update organization", Description = "Updates organization name and settings")]
    [SwaggerResponse(200, "Organization updated", typeof(OrganizationDetailDto))]
    [SwaggerResponse(403, "Insufficient permissions")]
    [SwaggerResponse(404, "Organization not found")]
    public async Task<IActionResult> UpdateOrganization(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.OrgSettings, cancellationToken))
        {
            return Forbid();
        }

        var result = await _organizationService.UpdateOrganizationAsync(
            id, request.Name, request.Settings, cancellationToken);

        if (!result.Success)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(MapToDetailDto(result.Organization!));
    }

    /// <summary>
    /// Delete (deactivate) organization
    /// </summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete organization", Description = "Soft-deletes the organization (owner only)")]
    [SwaggerResponse(204, "Organization deleted")]
    [SwaggerResponse(403, "Only owner can delete")]
    [SwaggerResponse(404, "Organization not found")]
    public async Task<IActionResult> DeleteOrganization(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.OrgDelete, cancellationToken))
        {
            return Forbid();
        }

        var success = await _organizationService.DeleteOrganizationAsync(id, cancellationToken);

        if (!success)
        {
            return NotFound();
        }

        Logger.LogInformation("Organization deleted: {OrgId} by user {UserId}", id, userId);

        return NoContent();
    }

    // Member Management

    /// <summary>
    /// Get organization members
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [SwaggerOperation(Summary = "Get members", Description = "Returns all members of the organization")]
    [SwaggerResponse(200, "List of members", typeof(List<MemberDto>))]
    [SwaggerResponse(403, "Insufficient permissions")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersView, cancellationToken))
        {
            return Forbid();
        }

        var members = await _organizationService.GetMembersAsync(id, cancellationToken);

        return Ok(members.Select(m => new MemberDto(
            m.Id, m.UserId, m.Email, m.DisplayName,
            m.RoleId, m.RoleName, m.JoinedAt, m.Permissions
        )));
    }

    /// <summary>
    /// Add a member to the organization
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [SwaggerOperation(Summary = "Add member", Description = "Adds a user to the organization")]
    [SwaggerResponse(201, "Member added", typeof(MemberDto))]
    [SwaggerResponse(400, "User is already a member")]
    [SwaggerResponse(403, "Insufficient permissions")]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersInvite, cancellationToken))
        {
            return Forbid();
        }

        // Find user by email
        var targetUser = await _authService.GetUserByEmailAsync(request.Email, cancellationToken);
        if (targetUser == null)
        {
            return BadRequest(new { error = "User not found with this email" });
        }

        var result = await _organizationService.AddMemberAsync(
            id, targetUser.Id, request.RoleId, userId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        Logger.LogInformation("Member added: {NewUserId} to org {OrgId} by {UserId}", targetUser.Id, id, userId);

        return CreatedAtAction(
            nameof(GetMembers),
            new { id },
            new MemberDto(
                result.Member!.Id, result.Member.UserId, result.Member.Email,
                result.Member.DisplayName, result.Member.RoleId, result.Member.RoleName,
                result.Member.JoinedAt, result.Member.Permissions
            ));
    }

    /// <summary>
    /// Update a member's role
    /// </summary>
    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [SwaggerOperation(Summary = "Update member role", Description = "Changes a member's role in the organization")]
    [SwaggerResponse(200, "Member role updated", typeof(MemberDto))]
    [SwaggerResponse(403, "Insufficient permissions")]
    [SwaggerResponse(404, "Member not found")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid id,
        Guid memberId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersManageRoles, cancellationToken))
        {
            return Forbid();
        }

        var result = await _organizationService.UpdateMemberRoleAsync(id, memberId, request.RoleId, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Member not found")
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error });
        }

        return Ok(new MemberDto(
            result.Member!.Id, result.Member.UserId, result.Member.Email,
            result.Member.DisplayName, result.Member.RoleId, result.Member.RoleName,
            result.Member.JoinedAt, result.Member.Permissions
        ));
    }

    /// <summary>
    /// Remove a member from the organization
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [SwaggerOperation(Summary = "Remove member", Description = "Removes a member from the organization")]
    [SwaggerResponse(204, "Member removed")]
    [SwaggerResponse(403, "Insufficient permissions or cannot remove owner")]
    [SwaggerResponse(404, "Member not found")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersRemove, cancellationToken))
        {
            return Forbid();
        }

        var success = await _organizationService.RemoveMemberAsync(id, memberId, cancellationToken);

        if (!success)
        {
            return NotFound(new { error = "Member not found or cannot be removed" });
        }

        Logger.LogInformation("Member removed: {MemberId} from org {OrgId} by {UserId}", memberId, id, userId);

        return NoContent();
    }

    // Role Management

    /// <summary>
    /// Get organization roles
    /// </summary>
    [HttpGet("{id:guid}/roles")]
    [SwaggerOperation(Summary = "Get roles", Description = "Returns all roles in the organization")]
    [SwaggerResponse(200, "List of roles", typeof(List<RoleDto>))]
    [SwaggerResponse(403, "Insufficient permissions")]
    public async Task<IActionResult> GetRoles(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.IsMemberAsync(userId, id, cancellationToken))
        {
            return Forbid();
        }

        var roles = await _organizationService.GetRolesAsync(id, cancellationToken);

        return Ok(roles.Select(r => new RoleDto(
            r.Id, r.Name, r.Description, r.IsSystem, r.Permissions, r.MemberCount
        )));
    }

    /// <summary>
    /// Create a custom role
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [SwaggerOperation(Summary = "Create role", Description = "Creates a custom role in the organization")]
    [SwaggerResponse(201, "Role created", typeof(RoleDto))]
    [SwaggerResponse(400, "Invalid role or duplicate name")]
    [SwaggerResponse(403, "Insufficient permissions")]
    public async Task<IActionResult> CreateRole(
        Guid id,
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersManageRoles, cancellationToken))
        {
            return Forbid();
        }

        var result = await _organizationService.CreateRoleAsync(
            id, request.Name, request.Description, request.Permissions, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        Logger.LogInformation("Role created: {RoleId} in org {OrgId} by {UserId}", result.Role!.Id, id, userId);

        return CreatedAtAction(
            nameof(GetRoles),
            new { id },
            new RoleDto(
                result.Role.Id, result.Role.Name, result.Role.Description,
                result.Role.IsSystem, result.Role.Permissions, result.Role.MemberCount
            ));
    }

    /// <summary>
    /// Update a custom role
    /// </summary>
    [HttpPut("{id:guid}/roles/{roleId:guid}")]
    [SwaggerOperation(Summary = "Update role", Description = "Updates a custom role (system roles cannot be modified)")]
    [SwaggerResponse(200, "Role updated", typeof(RoleDto))]
    [SwaggerResponse(400, "Cannot modify system role")]
    [SwaggerResponse(403, "Insufficient permissions")]
    [SwaggerResponse(404, "Role not found")]
    public async Task<IActionResult> UpdateRole(
        Guid id,
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersManageRoles, cancellationToken))
        {
            return Forbid();
        }

        // Verify role belongs to this organization
        var existingRole = await _organizationService.GetRoleAsync(roleId, cancellationToken);
        if (existingRole == null)
        {
            return NotFound(new { error = "Role not found" });
        }

        var result = await _organizationService.UpdateRoleAsync(
            roleId, request.Name, request.Description, request.Permissions, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new RoleDto(
            result.Role!.Id, result.Role.Name, result.Role.Description,
            result.Role.IsSystem, result.Role.Permissions, result.Role.MemberCount
        ));
    }

    /// <summary>
    /// Delete a custom role
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [SwaggerOperation(Summary = "Delete role", Description = "Deletes a custom role (must have no members)")]
    [SwaggerResponse(204, "Role deleted")]
    [SwaggerResponse(400, "Cannot delete system role or role has members")]
    [SwaggerResponse(403, "Insufficient permissions")]
    public async Task<IActionResult> DeleteRole(Guid id, Guid roleId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (!await _organizationService.HasPermissionAsync(userId, id, Permissions.MembersManageRoles, cancellationToken))
        {
            return Forbid();
        }

        var success = await _organizationService.DeleteRoleAsync(roleId, cancellationToken);

        if (!success)
        {
            return BadRequest(new { error = "Cannot delete role (may be system role or has members)" });
        }

        Logger.LogInformation("Role deleted: {RoleId} from org {OrgId} by {UserId}", roleId, id, userId);

        return NoContent();
    }

    /// <summary>
    /// Get available permissions
    /// </summary>
    [HttpGet("permissions")]
    [SwaggerOperation(Summary = "Get available permissions", Description = "Returns all available permissions that can be assigned to roles")]
    [SwaggerResponse(200, "List of permission strings", typeof(List<string>))]
    public IActionResult GetAvailablePermissions()
    {
        return Ok(Permissions.All);
    }

    private static OrganizationDetailDto MapToDetailDto(OrganizationDetail org)
    {
        return new OrganizationDetailDto(
            org.Id,
            org.Name,
            org.Slug,
            org.OwnerId,
            org.OwnerName,
            org.CreatedAt,
            org.IsActive,
            org.Settings,
            org.Members.Select(m => new MemberDto(
                m.Id, m.UserId, m.Email, m.DisplayName,
                m.RoleId, m.RoleName, m.JoinedAt, m.Permissions
            )).ToList(),
            org.Roles.Select(r => new RoleDto(
                r.Id, r.Name, r.Description, r.IsSystem, r.Permissions, r.MemberCount
            )).ToList()
        );
    }
}
