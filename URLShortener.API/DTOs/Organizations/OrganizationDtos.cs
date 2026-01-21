using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.DTOs.Organizations;

// Request DTOs
public record CreateOrganizationRequest(
    [Required][MaxLength(100)] string Name,
    [Required][MaxLength(50)][RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must be lowercase alphanumeric with hyphens only")] string Slug
);

public record UpdateOrganizationRequest(
    [Required][MaxLength(100)] string Name,
    Dictionary<string, object>? Settings
);

public record InviteMemberRequest(
    [Required][EmailAddress] string Email,
    [Required] Guid RoleId
);

public record UpdateMemberRoleRequest(
    [Required] Guid RoleId
);

public record CreateRoleRequest(
    [Required][MaxLength(50)] string Name,
    [MaxLength(256)] string? Description,
    [Required] List<string> Permissions
);

public record UpdateRoleRequest(
    [MaxLength(50)] string? Name,
    [MaxLength(256)] string? Description,
    List<string>? Permissions
);

// Response DTOs
public record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAt,
    bool IsActive,
    int MemberCount,
    int UrlCount
);

public record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedAt,
    bool IsActive,
    Dictionary<string, object> Settings,
    List<MemberDto> Members,
    List<RoleDto> Roles
);

public record MemberDto(
    Guid Id,
    Guid UserId,
    string Email,
    string DisplayName,
    Guid RoleId,
    string RoleName,
    DateTime JoinedAt,
    List<string> Permissions
);

public record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem,
    List<string> Permissions,
    int MemberCount
);

public record InvitationDto(
    Guid InvitationId,
    string Email,
    string OrganizationName,
    string InvitedBy,
    DateTime ExpiresAt
);
