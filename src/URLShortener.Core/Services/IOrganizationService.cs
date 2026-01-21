namespace URLShortener.Core.Services;

public interface IOrganizationService
{
    // Organization CRUD
    Task<OrganizationResult> CreateOrganizationAsync(Guid ownerId, string name, string slug, CancellationToken cancellationToken = default);
    Task<OrganizationResult> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationResult> GetOrganizationBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<List<OrganizationSummary>> GetUserOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResult> UpdateOrganizationAsync(Guid organizationId, string name, Dictionary<string, object>? settings, CancellationToken cancellationToken = default);
    Task<bool> DeleteOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    // Member management
    Task<MemberResult> AddMemberAsync(Guid organizationId, Guid userId, Guid roleId, Guid invitedById, CancellationToken cancellationToken = default);
    Task<MemberResult> UpdateMemberRoleAsync(Guid organizationId, Guid memberId, Guid newRoleId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(Guid organizationId, Guid memberId, CancellationToken cancellationToken = default);
    Task<List<MemberInfo>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);

    // Role management
    Task<RoleResult> CreateRoleAsync(Guid organizationId, string name, string? description, List<string> permissions, CancellationToken cancellationToken = default);
    Task<RoleResult> UpdateRoleAsync(Guid roleId, string? name, string? description, List<string>? permissions, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<List<RoleInfo>> GetRolesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<RoleInfo?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    // Permission checking
    Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnerAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
}

public record OrganizationResult(
    bool Success,
    OrganizationDetail? Organization,
    string? Error
);

public record OrganizationDetail(
    Guid Id,
    string Name,
    string Slug,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAt,
    bool IsActive,
    Dictionary<string, object> Settings,
    List<MemberInfo> Members,
    List<RoleInfo> Roles,
    int UrlCount
);

public record OrganizationSummary(
    Guid Id,
    string Name,
    string Slug,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAt,
    bool IsActive,
    int MemberCount,
    int UrlCount,
    string UserRole,
    List<string> UserPermissions
);

public record MemberResult(
    bool Success,
    MemberInfo? Member,
    string? Error
);

public record MemberInfo(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    Guid RoleId,
    string RoleName,
    DateTime JoinedAt,
    List<string> Permissions
);

public record RoleResult(
    bool Success,
    RoleInfo? Role,
    string? Error
);

public record RoleInfo(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem,
    List<string> Permissions,
    int MemberCount
);
