using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using URLShortener.Core.Services;
using URLShortener.Infrastructure.Data;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UrlShortenerDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const int BcryptWorkFactor = 12;

    public AuthService(
        UrlShortenerDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUser = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (existingUser)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", email);
            return new AuthResult(false, null, null, null, null, null, "Email already registered");
        }

        // Create user
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered successfully: {UserId}", user.Id);

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var (refreshToken, refreshTokenHash) = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());

        // Save refresh token
        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshTokenExpiry,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "registration"
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            Success: true,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            RefreshTokenExpiry: refreshTokenExpiry,
            User: MapToUserInfo(user, new List<OrganizationMembership>()),
            Error: null
        );
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Organization)
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", email);
            return new AuthResult(false, null, null, null, null, null, "Invalid email or password");
        }

        // Check if account is locked
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Login attempt for locked account: {UserId}", user.Id);
            return new AuthResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                AccessTokenExpiry: null,
                RefreshTokenExpiry: null,
                User: null,
                Error: "Account is locked due to too many failed login attempts",
                IsLockedOut: true,
                LockoutEndAt: user.LockoutEndAt
            );
        }

        // Check if account is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {UserId}", user.Id);
            return new AuthResult(false, null, null, null, null, null, "Account is deactivated");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEndAt = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("Account locked due to failed attempts: {UserId}", user.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Failed login attempt for user: {UserId}", user.Id);
            return new AuthResult(false, null, null, null, null, null, "Invalid email or password");
        }

        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var (refreshToken, refreshTokenHash) = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());

        // Save refresh token
        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshTokenExpiry,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var memberships = MapOrganizationMemberships(user.OrganizationMemberships);

        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

        return new AuthResult(
            Success: true,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            RefreshTokenExpiry: refreshTokenExpiry,
            User: MapToUserInfo(user, memberships),
            Error: null
        );
    }

    public async Task<AuthResult> RefreshTokenAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);

        var storedToken = await _context.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.OrganizationMemberships)
                    .ThenInclude(m => m.Organization)
            .Include(t => t.User)
                .ThenInclude(u => u.OrganizationMemberships)
                    .ThenInclude(m => m.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found");
            return new AuthResult(false, null, null, null, null, null, "Invalid refresh token");
        }

        if (!storedToken.IsActive)
        {
            // Token reuse detection - revoke all tokens for this user
            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Attempted reuse of revoked refresh token for user: {UserId}", storedToken.UserId);
                await RevokeAllUserTokensAsync(storedToken.UserId, ipAddress, "Token reuse detected", cancellationToken);
            }

            return new AuthResult(false, null, null, null, null, null, "Invalid refresh token");
        }

        var user = storedToken.User;

        if (!user.IsActive)
        {
            return new AuthResult(false, null, null, null, null, null, "Account is deactivated");
        }

        // Revoke old token
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.ReasonRevoked = "Replaced by new token";

        // Generate new tokens
        var accessToken = GenerateAccessToken(user);
        var (newRefreshToken, newRefreshTokenHash) = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());

        // Save new refresh token
        var newRefreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = refreshTokenExpiry,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        storedToken.ReplacedByTokenId = newRefreshTokenEntity.Id;

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var memberships = MapOrganizationMemberships(user.OrganizationMemberships);

        _logger.LogInformation("Token refreshed for user: {UserId}", user.Id);

        return new AuthResult(
            Success: true,
            AccessToken: accessToken,
            RefreshToken: newRefreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            RefreshTokenExpiry: refreshTokenExpiry,
            User: MapToUserInfo(user, memberships),
            Error: null
        );
    }

    public async Task<bool> RevokeTokenAsync(
        string refreshToken,
        string ipAddress,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (storedToken == null || !storedToken.IsActive)
        {
            return false;
        }

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.ReasonRevoked = reason;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token revoked for user: {UserId}", storedToken.UserId);
        return true;
    }

    public async Task<bool> RevokeAllUserTokensAsync(
        Guid userId,
        string ipAddress,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReasonRevoked = reason;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All tokens revoked for user: {UserId}, Count: {Count}", userId, activeTokens.Count);
        return true;
    }

    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Organization)
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return null;

        var memberships = MapOrganizationMemberships(user.OrganizationMemberships);
        return MapToUserInfo(user, memberships);
    }

    public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Organization)
            .Include(u => u.OrganizationMemberships)
                .ThenInclude(m => m.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user == null) return null;

        var memberships = MapOrganizationMemberships(user.OrganizationMemberships);
        return MapToUserInfo(user, memberships);
    }

    public async Task<bool> UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null) return false;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string GenerateAccessToken(UserEntity user)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "URLShortener";
        var audience = _configuration["Jwt:Audience"] ?? "urlshortener-api";
        var expirationMinutes = GetAccessTokenExpirationMinutes();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("display_name", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string token, string hash) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes);
        var hash = HashToken(token);
        return (token, hash);
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private int GetAccessTokenExpirationMinutes()
    {
        return _configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 15);
    }

    private int GetRefreshTokenExpirationDays()
    {
        return _configuration.GetValue("Jwt:RefreshTokenExpirationDays", 7);
    }

    private static UserInfo MapToUserInfo(UserEntity user, List<OrganizationMembership> memberships)
    {
        return new UserInfo(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            IsActive: user.IsActive,
            Organizations: memberships
        );
    }

    private static List<OrganizationMembership> MapOrganizationMemberships(
        ICollection<OrganizationMemberEntity> memberships)
    {
        return memberships.Select(m => new OrganizationMembership(
            OrganizationId: m.OrganizationId,
            OrganizationName: m.Organization?.Name ?? "",
            OrganizationSlug: m.Organization?.Slug ?? "",
            RoleId: m.RoleId,
            RoleName: m.Role?.Name ?? "",
            Permissions: m.Role?.Permissions ?? new List<string>()
        )).ToList();
    }
}
