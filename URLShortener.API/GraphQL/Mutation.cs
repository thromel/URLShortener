using HotChocolate;
using HotChocolate.Authorization;
using MediatR;
using URLShortener.API.GraphQL.Types;
using URLShortener.Core.CQRS.Commands;

namespace URLShortener.API.GraphQL;

/// <summary>
/// GraphQL Mutation root type for URL Shortener API.
/// </summary>
public class Mutation
{
    /// <summary>
    /// Creates a new shortened URL.
    /// </summary>
    [Authorize]
    public async Task<CreateUrlPayload> CreateUrl(
        CreateUrlInput input,
        [Service] IMediator mediator,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        try
        {
            var userIdString = httpContextAccessor.HttpContext?.User?.Identity?.Name;
            Guid.TryParse(userIdString, out var userId);

            var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";

            var command = new CreateShortUrlCommand
            {
                OriginalUrl = input.OriginalUrl,
                CustomAlias = input.CustomAlias,
                ExpiresAt = input.ExpiresAt,
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Metadata = input.Metadata
            };

            var result = await mediator.Send(command);

            return new CreateUrlPayload
            {
                Url = new ShortUrlType
                {
                    ShortCode = result.ShortCode,
                    ShortUrl = result.ShortUrl,
                    OriginalUrl = result.OriginalUrl,
                    CreatedAt = result.CreatedAt,
                    ExpiresAt = result.ExpiresAt,
                    UserId = result.UserId.ToString(),
                    Status = "Active",
                    AccessCount = 0
                }
            };
        }
        catch (ArgumentException ex)
        {
            return new CreateUrlPayload
            {
                Errors = new List<string> { ex.Message }
            };
        }
        catch (Exception)
        {
            return new CreateUrlPayload
            {
                Errors = new List<string> { "An unexpected error occurred while creating the URL." }
            };
        }
    }

    /// <summary>
    /// Deletes a shortened URL.
    /// </summary>
    [Authorize]
    public async Task<DeleteUrlPayload> DeleteUrl(
        string shortCode,
        [Service] IMediator mediator,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        try
        {
            var userIdString = httpContextAccessor.HttpContext?.User?.Identity?.Name;
            Guid.TryParse(userIdString, out var userId);
            var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var command = new DeleteUrlCommand
            {
                ShortCode = shortCode,
                UserId = userId,
                IpAddress = ipAddress
            };

            var success = await mediator.Send(command);

            return new DeleteUrlPayload
            {
                ShortCode = shortCode,
                Success = success,
                Error = success ? null : "URL not found or you don't have permission to delete it."
            };
        }
        catch (Exception)
        {
            return new DeleteUrlPayload
            {
                ShortCode = shortCode,
                Success = false,
                Error = "An unexpected error occurred while deleting the URL."
            };
        }
    }

    /// <summary>
    /// Disables a shortened URL (soft delete).
    /// </summary>
    [Authorize]
    public async Task<DisableUrlPayload> DisableUrl(
        string shortCode,
        [Service] IMediator mediator,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        try
        {
            var userIdString = httpContextAccessor.HttpContext?.User?.Identity?.Name;
            Guid.TryParse(userIdString, out var userId);
            var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var command = new DisableUrlCommand
            {
                ShortCode = shortCode,
                AdminUserId = userId,
                AdminIpAddress = ipAddress
            };

            await mediator.Send(command);

            return new DisableUrlPayload
            {
                ShortCode = shortCode,
                Success = true
            };
        }
        catch (Exception)
        {
            return new DisableUrlPayload
            {
                ShortCode = shortCode,
                Success = false,
                Error = "Failed to disable URL."
            };
        }
    }

    /// <summary>
    /// Creates multiple URLs in a single operation.
    /// </summary>
    [Authorize]
    public async Task<BulkCreatePayload> BulkCreateUrls(
        List<CreateUrlInput> inputs,
        [Service] IMediator mediator,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (inputs.Count > 100)
        {
            return new BulkCreatePayload
            {
                Results = new List<CreateUrlPayload>(),
                TotalRequested = inputs.Count,
                TotalCreated = 0,
                Error = "Maximum 100 URLs can be created in a single request."
            };
        }

        var userIdString = httpContextAccessor.HttpContext?.User?.Identity?.Name;
        Guid.TryParse(userIdString, out var userId);

        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";

        var results = new List<CreateUrlPayload>();

        foreach (var input in inputs)
        {
            try
            {
                var command = new CreateShortUrlCommand
                {
                    OriginalUrl = input.OriginalUrl,
                    CustomAlias = input.CustomAlias,
                    ExpiresAt = input.ExpiresAt,
                    UserId = userId,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Metadata = input.Metadata
                };

                var result = await mediator.Send(command);

                results.Add(new CreateUrlPayload
                {
                    Url = new ShortUrlType
                    {
                        ShortCode = result.ShortCode,
                        ShortUrl = result.ShortUrl,
                        OriginalUrl = result.OriginalUrl,
                        CreatedAt = result.CreatedAt,
                        ExpiresAt = result.ExpiresAt,
                        UserId = result.UserId.ToString(),
                        Status = "Active",
                        AccessCount = 0
                    }
                });
            }
            catch (Exception ex)
            {
                results.Add(new CreateUrlPayload
                {
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        return new BulkCreatePayload
        {
            Results = results,
            TotalRequested = inputs.Count,
            TotalCreated = results.Count(r => r.Success)
        };
    }
}

/// <summary>
/// Result type for URL disable mutation.
/// </summary>
public class DisableUrlPayload
{
    public string ShortCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result type for bulk URL creation.
/// </summary>
public class BulkCreatePayload
{
    public List<CreateUrlPayload> Results { get; set; } = new();
    public int TotalRequested { get; set; }
    public int TotalCreated { get; set; }
    public string? Error { get; set; }
}
