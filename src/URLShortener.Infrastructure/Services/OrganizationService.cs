using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using URLShortener.Core.Services;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly UrlShortenerDbContext _context;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(UrlShortenerDbContext context, ILogger<OrganizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrganizationResult> CreateOrganizationAsync(
        Guid ownerId,
        string name,
        string slug,
        CancellationToken cancellationToken = default)
    {
        // Check if slug already exists
        var slugExists = await _context.Organizations
            .AnyAsync(o => o.Slug.ToLower() == slug.ToLower(), cancellationToken);

        if (slugExists)
        {
            return new OrganizationResult(false, null, "Organization slug already exists");
        }

        var owner = await _context.Users.FindAsync(new object[] { ownerId }, cancellationToken);
        if (owner == null)
        {
            return new OrganizationResult(false, null, "Owner not found");
        }

        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLower(),
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Settings = new Dictionary<string, object>()
        };

        _context.Organizations.Add(organization);

        // Create default roles for the organization
        var ownerRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = SystemRoles.Owner,
            Description = "Organization owner with full access",
            IsSystem = true,
            Permissions = Permissions.OwnerPermissions.ToList(),
            CreatedAt = DateTime.UtcNow
        };

        var adminRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = SystemRoles.Admin,
            Description = "Administrator with management access",
            IsSystem = true,
            Permissions = Permissions.AdminPermissions.ToList(),
            CreatedAt = DateTime.UtcNow
        };

        var memberRole = new RoleEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = SystemRoles.Member,
            Description = "Regular member with basic access",
            IsSystem = true,
            Permissions = Permissions.MemberPermissions.ToList(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Roles.AddRange(ownerRole, adminRole, memberRole);

        // Add owner as first member with Owner role
        var ownerMembership = new OrganizationMemberEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = ownerId,
            RoleId = ownerRole.Id,
            JoinedAt = DateTime.UtcNow
        };

        _context.OrganizationMembers.Add(ownerMembership);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization created: {OrganizationId} by user {UserId}", organization.Id, ownerId);

        return new OrganizationResult(
            true,
            new OrganizationDetail(
                organization.Id,
                organization.Name,
                organization.Slug,
                ownerId,
                owner.DisplayName,
                organization.CreatedAt,
                organization.IsActive,
                organization.Settings,
                new List<MemberInfo>
                {
                    new(ownerMembership.Id, ownerId, owner.Email, owner.DisplayName, ownerRole.Id, ownerRole.Name, ownerMembership.JoinedAt, ownerRole.Permissions)
                },
                new List<RoleInfo>
                {
                    new(ownerRole.Id, ownerRole.Name, ownerRole.Description, true, ownerRole.Permissions, 1),
                    new(adminRole.Id, adminRole.Name, adminRole.Description, true, adminRole.Permissions, 0),
                    new(memberRole.Id, memberRole.Name, memberRole.Description, true, memberRole.Permissions, 0)
                },
                0
            ),
            null
        );
    }

    public async Task<OrganizationResult> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .Include(o => o.Owner)
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
            .Include(o => o.Members)
                .ThenInclude(m => m.Role)
            .Include(o => o.Roles)
                .ThenInclude(r => r.Members)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization == null)
        {
            return new OrganizationResult(false, null, "Organization not found");
        }

        var urlCount = await _context.Urls.CountAsync(u => u.OrganizationId == organizationId, cancellationToken);

        return new OrganizationResult(true, MapToOrganizationDetail(organization, urlCount), null);
    }

    public async Task<OrganizationResult> GetOrganizationBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .Include(o => o.Owner)
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
            .Include(o => o.Members)
                .ThenInclude(m => m.Role)
            .Include(o => o.Roles)
                .ThenInclude(r => r.Members)
            .FirstOrDefaultAsync(o => o.Slug.ToLower() == slug.ToLower(), cancellationToken);

        if (organization == null)
        {
            return new OrganizationResult(false, null, "Organization not found");
        }

        var urlCount = await _context.Urls.CountAsync(u => u.OrganizationId == organization.Id, cancellationToken);

        return new OrganizationResult(true, MapToOrganizationDetail(organization, urlCount), null);
    }

    public async Task<List<OrganizationSummary>> GetUserOrganizationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _context.OrganizationMembers
            .Include(m => m.Organization)
                .ThenInclude(o => o.Owner)
            .Include(m => m.Organization)
                .ThenInclude(o => o.Members)
            .Include(m => m.Role)
            .Where(m => m.UserId == userId && m.Organization.IsActive)
            .ToListAsync(cancellationToken);

        var result = new List<OrganizationSummary>();

        foreach (var membership in memberships)
        {
            var urlCount = await _context.Urls.CountAsync(u => u.OrganizationId == membership.OrganizationId, cancellationToken);

            result.Add(new OrganizationSummary(
                membership.Organization.Id,
                membership.Organization.Name,
                membership.Organization.Slug,
                membership.Organization.OwnerId,
                membership.Organization.Owner.DisplayName,
                membership.Organization.CreatedAt,
                membership.Organization.IsActive,
                membership.Organization.Members.Count,
                urlCount,
                membership.Role.Name,
                membership.Role.Permissions
            ));
        }

        return result;
    }

    public async Task<OrganizationResult> UpdateOrganizationAsync(
        Guid organizationId,
        string name,
        Dictionary<string, object>? settings,
        CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .Include(o => o.Owner)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization == null)
        {
            return new OrganizationResult(false, null, "Organization not found");
        }

        organization.Name = name;
        if (settings != null)
        {
            organization.Settings = settings;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetOrganizationAsync(organizationId, cancellationToken);
    }

    public async Task<bool> DeleteOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);

        if (organization == null)
        {
            return false;
        }

        // Soft delete - just mark as inactive
        organization.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization soft-deleted: {OrganizationId}", organizationId);

        return true;
    }

    public async Task<MemberResult> AddMemberAsync(
        Guid organizationId,
        Guid userId,
        Guid roleId,
        Guid invitedById,
        CancellationToken cancellationToken = default)
    {
        // Check if user is already a member
        var existingMembership = await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (existingMembership)
        {
            return new MemberResult(false, null, "User is already a member of this organization");
        }

        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            return new MemberResult(false, null, "User not found");
        }

        var role = await _context.Roles.FindAsync(new object[] { roleId }, cancellationToken);
        if (role == null || role.OrganizationId != organizationId)
        {
            return new MemberResult(false, null, "Invalid role for this organization");
        }

        // Cannot assign Owner role to additional members
        if (role.Name == SystemRoles.Owner)
        {
            return new MemberResult(false, null, "Cannot assign Owner role to additional members");
        }

        var membership = new OrganizationMemberEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            RoleId = roleId,
            JoinedAt = DateTime.UtcNow,
            InvitedById = invitedById
        };

        _context.OrganizationMembers.Add(membership);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Member added to organization: {UserId} to {OrganizationId}", userId, organizationId);

        return new MemberResult(
            true,
            new MemberInfo(membership.Id, userId, user.Email, user.DisplayName, roleId, role.Name, membership.JoinedAt, role.Permissions),
            null
        );
    }

    public async Task<MemberResult> UpdateMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        Guid newRoleId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.OrganizationMembers
            .Include(m => m.User)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, cancellationToken);

        if (membership == null)
        {
            return new MemberResult(false, null, "Member not found");
        }

        // Cannot change owner's role
        var organization = await _context.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);
        if (organization != null && membership.UserId == organization.OwnerId)
        {
            return new MemberResult(false, null, "Cannot change the owner's role");
        }

        var newRole = await _context.Roles.FindAsync(new object[] { newRoleId }, cancellationToken);
        if (newRole == null || newRole.OrganizationId != organizationId)
        {
            return new MemberResult(false, null, "Invalid role for this organization");
        }

        // Cannot assign Owner role
        if (newRole.Name == SystemRoles.Owner)
        {
            return new MemberResult(false, null, "Cannot assign Owner role");
        }

        membership.RoleId = newRoleId;
        await _context.SaveChangesAsync(cancellationToken);

        return new MemberResult(
            true,
            new MemberInfo(membership.Id, membership.UserId, membership.User.Email, membership.User.DisplayName, newRoleId, newRole.Name, membership.JoinedAt, newRole.Permissions),
            null
        );
    }

    public async Task<bool> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, cancellationToken);

        if (membership == null)
        {
            return false;
        }

        // Cannot remove owner
        var organization = await _context.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);
        if (organization != null && membership.UserId == organization.OwnerId)
        {
            return false;
        }

        _context.OrganizationMembers.Remove(membership);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Member removed from organization: {MemberId} from {OrganizationId}", memberId, organizationId);

        return true;
    }

    public async Task<List<MemberInfo>> GetMembersAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var members = await _context.OrganizationMembers
            .Include(m => m.User)
            .Include(m => m.Role)
            .Where(m => m.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        return members.Select(m => new MemberInfo(
            m.Id,
            m.UserId,
            m.User.Email,
            m.User.DisplayName,
            m.RoleId,
            m.Role.Name,
            m.JoinedAt,
            m.Role.Permissions
        )).ToList();
    }

    public async Task<RoleResult> CreateRoleAsync(
        Guid organizationId,
        string name,
        string? description,
        List<string> permissions,
        CancellationToken cancellationToken = default)
    {
        // Check if role name already exists in this organization
        var roleExists = await _context.Roles
            .AnyAsync(r => r.OrganizationId == organizationId && r.Name.ToLower() == name.ToLower(), cancellationToken);

        if (roleExists)
        {
            return new RoleResult(false, null, "Role with this name already exists");
        }

        // Validate permissions
        var invalidPermissions = permissions.Except(Permissions.All).ToList();
        if (invalidPermissions.Any())
        {
            return new RoleResult(false, null, $"Invalid permissions: {string.Join(", ", invalidPermissions)}");
        }

        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Description = description ?? string.Empty,
            IsSystem = false,
            Permissions = permissions,
            CreatedAt = DateTime.UtcNow
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Role created: {RoleId} in {OrganizationId}", role.Id, organizationId);

        return new RoleResult(true, new RoleInfo(role.Id, role.Name, role.Description, false, permissions, 0), null);
    }

    public async Task<RoleResult> UpdateRoleAsync(
        Guid roleId,
        string? name,
        string? description,
        List<string>? permissions,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role == null)
        {
            return new RoleResult(false, null, "Role not found");
        }

        // Cannot modify system roles
        if (role.IsSystem)
        {
            return new RoleResult(false, null, "Cannot modify system roles");
        }

        if (name != null)
        {
            // Check if new name conflicts with existing role
            var nameConflict = await _context.Roles
                .AnyAsync(r => r.OrganizationId == role.OrganizationId && r.Id != roleId && r.Name.ToLower() == name.ToLower(), cancellationToken);

            if (nameConflict)
            {
                return new RoleResult(false, null, "Role with this name already exists");
            }

            role.Name = name;
        }

        if (description != null)
        {
            role.Description = description;
        }

        if (permissions != null)
        {
            var invalidPermissions = permissions.Except(Permissions.All).ToList();
            if (invalidPermissions.Any())
            {
                return new RoleResult(false, null, $"Invalid permissions: {string.Join(", ", invalidPermissions)}");
            }

            role.Permissions = permissions;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new RoleResult(true, new RoleInfo(role.Id, role.Name, role.Description, role.IsSystem, role.Permissions, role.Members.Count), null);
    }

    public async Task<bool> DeleteRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role == null)
        {
            return false;
        }

        // Cannot delete system roles
        if (role.IsSystem)
        {
            return false;
        }

        // Cannot delete role with members
        if (role.Members.Any())
        {
            return false;
        }

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Role deleted: {RoleId}", roleId);

        return true;
    }

    public async Task<List<RoleInfo>> GetRolesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .Include(r => r.Members)
            .Where(r => r.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleInfo(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystem,
            r.Permissions,
            r.Members.Count
        )).ToList();
    }

    public async Task<RoleInfo?> GetRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role == null)
        {
            return null;
        }

        return new RoleInfo(role.Id, role.Name, role.Description, role.IsSystem, role.Permissions, role.Members.Count);
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid organizationId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, organizationId, cancellationToken);
        return permissions.Contains(permission);
    }

    public async Task<List<string>> GetUserPermissionsAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _context.OrganizationMembers
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);

        if (membership == null)
        {
            return new List<string>();
        }

        return membership.Role.Permissions;
    }

    public async Task<bool> IsOwnerAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        return organization?.OwnerId == userId;
    }

    public async Task<bool> IsMemberAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OrganizationMembers
            .AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);
    }

    private static OrganizationDetail MapToOrganizationDetail(OrganizationEntity organization, int urlCount)
    {
        return new OrganizationDetail(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.OwnerId,
            organization.Owner.DisplayName,
            organization.CreatedAt,
            organization.IsActive,
            organization.Settings,
            organization.Members.Select(m => new MemberInfo(
                m.Id,
                m.UserId,
                m.User.Email,
                m.User.DisplayName,
                m.RoleId,
                m.Role.Name,
                m.JoinedAt,
                m.Role.Permissions
            )).ToList(),
            organization.Roles.Select(r => new RoleInfo(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.Permissions,
                r.Members.Count
            )).ToList(),
            urlCount
        );
    }
}
