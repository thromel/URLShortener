using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using URLShortener.API.DTOs.Auth;
using URLShortener.API.Services;
using URLShortener.Core.Services;

namespace URLShortener.API.Controllers;

/// <summary>
/// Authentication controller for user registration, login, and token management
/// </summary>
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;
    private const string RefreshTokenCookieName = "refreshToken";

    public AuthController(
        IAuthService authService,
        IClientInfoService clientInfoService,
        ILogger<AuthController> logger)
        : base(clientInfoService, logger)
    {
        _authService = authService;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Register a new user", Description = "Creates a new user account and returns authentication tokens")]
    [SwaggerResponse(200, "Registration successful", typeof(AuthResponse))]
    [SwaggerResponse(400, "Invalid request or email already exists")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RegisterAsync(
            request.Email,
            request.Password,
            request.DisplayName,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        SetRefreshTokenCookie(result.RefreshToken!, result.RefreshTokenExpiry!.Value);

        Logger.LogInformation("User registered: {Email}", request.Email);

        return Ok(MapToAuthResponse(result));
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Login with credentials", Description = "Authenticates user and returns access token")]
    [SwaggerResponse(200, "Login successful", typeof(AuthResponse))]
    [SwaggerResponse(400, "Invalid credentials")]
    [SwaggerResponse(423, "Account locked")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var ipAddress = GetClientIpAddress();
        var result = await _authService.LoginAsync(
            request.Email,
            request.Password,
            ipAddress,
            cancellationToken);

        if (!result.Success)
        {
            if (result.IsLockedOut)
            {
                return StatusCode(423, new
                {
                    error = result.Error,
                    lockoutEndAt = result.LockoutEndAt
                });
            }

            return BadRequest(new { error = result.Error });
        }

        SetRefreshTokenCookie(result.RefreshToken!, result.RefreshTokenExpiry!.Value);

        Logger.LogInformation("User logged in: {Email}", request.Email);

        return Ok(MapToAuthResponse(result));
    }

    /// <summary>
    /// Refresh the access token using the refresh token from cookie
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Refresh access token", Description = "Uses refresh token from cookie to get a new access token")]
    [SwaggerResponse(200, "Token refreshed", typeof(AuthResponse))]
    [SwaggerResponse(401, "Invalid or expired refresh token")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { error = "Refresh token not found" });
        }

        var ipAddress = GetClientIpAddress();
        var result = await _authService.RefreshTokenAsync(refreshToken, ipAddress, cancellationToken);

        if (!result.Success)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new { error = result.Error });
        }

        SetRefreshTokenCookie(result.RefreshToken!, result.RefreshTokenExpiry!.Value);

        return Ok(MapToAuthResponse(result));
    }

    /// <summary>
    /// Logout and revoke the current refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [SwaggerOperation(Summary = "Logout user", Description = "Revokes the current refresh token")]
    [SwaggerResponse(200, "Logged out successfully")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var ipAddress = GetClientIpAddress();
            await _authService.RevokeTokenAsync(refreshToken, ipAddress, "User logout", cancellationToken);
        }

        ClearRefreshTokenCookie();

        Logger.LogInformation("User logged out: {UserId}", GetUserId());

        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Revoke all refresh tokens for the current user
    /// </summary>
    [HttpPost("revoke-all")]
    [Authorize]
    [SwaggerOperation(Summary = "Revoke all tokens", Description = "Revokes all refresh tokens for the current user (logout from all devices)")]
    [SwaggerResponse(200, "All tokens revoked")]
    public async Task<IActionResult> RevokeAllTokens(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var ipAddress = GetClientIpAddress();

        await _authService.RevokeAllUserTokensAsync(userId, ipAddress, "User requested revocation", cancellationToken);

        ClearRefreshTokenCookie();

        Logger.LogInformation("All tokens revoked for user: {UserId}", userId);

        return Ok(new { message = "All tokens revoked successfully" });
    }

    /// <summary>
    /// Get the current authenticated user's information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(Summary = "Get current user", Description = "Returns the current authenticated user's information")]
    [SwaggerResponse(200, "User information", typeof(UserDto))]
    [SwaggerResponse(401, "Not authenticated")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var user = await _authService.GetUserByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(MapToUserDto(user));
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/api"
        };

        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api"
        });
    }

    private static AuthResponse MapToAuthResponse(AuthResult result)
    {
        return new AuthResponse(
            AccessToken: result.AccessToken!,
            ExpiresAt: result.AccessTokenExpiry!.Value,
            User: MapToUserDto(result.User!)
        );
    }

    private static UserDto MapToUserDto(UserInfo user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            CreatedAt: user.CreatedAt,
            Organizations: user.Organizations.Select(o => new OrganizationMembershipDto(
                OrganizationId: o.OrganizationId,
                OrganizationName: o.OrganizationName,
                OrganizationSlug: o.OrganizationSlug,
                RoleName: o.RoleName,
                Permissions: o.Permissions
            )).ToList()
        );
    }
}
