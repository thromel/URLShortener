using System.ComponentModel.DataAnnotations;

namespace URLShortener.API.DTOs.Auth;

// Request DTOs
public record RegisterRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(8)][MaxLength(100)] string Password,
    [Required][MaxLength(100)] string DisplayName
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record RefreshTokenRequest(
    [Required] string RefreshToken
);

// Response DTOs
public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt,
    List<OrganizationMembershipDto> Organizations
);

public record OrganizationMembershipDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string RoleName,
    List<string> Permissions
);

public record TokenValidationResult(
    bool IsValid,
    Guid? UserId,
    string? Email,
    string? Error
);
