namespace URLShortener.Infrastructure.Data.Entities;

public class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndAt { get; set; }

    // Navigation properties
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
    public ICollection<OrganizationMemberEntity> OrganizationMemberships { get; set; } = new List<OrganizationMemberEntity>();
    public ICollection<OrganizationEntity> OwnedOrganizations { get; set; } = new List<OrganizationEntity>();
    public ICollection<UrlEntity> Urls { get; set; } = new List<UrlEntity>();
}
