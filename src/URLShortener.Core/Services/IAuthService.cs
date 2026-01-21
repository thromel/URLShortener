namespace URLShortener.Core.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(string email, string password, string ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress, string reason, CancellationToken cancellationToken = default);
    Task<bool> RevokeAllUserTokensAsync(Guid userId, string ipAddress, string reason, CancellationToken cancellationToken = default);
    Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default);
}

public record AuthResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? AccessTokenExpiry,
    DateTime? RefreshTokenExpiry,
    UserInfo? User,
    string? Error,
    bool IsLockedOut = false,
    DateTime? LockoutEndAt = null
);

public record UserInfo(
    Guid Id,
    string Email,
    string DisplayName,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool IsActive,
    List<OrganizationMembership> Organizations
);

public record OrganizationMembership(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    Guid RoleId,
    string RoleName,
    List<string> Permissions
);
