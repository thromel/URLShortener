namespace URLShortener.Infrastructure.Data.Entities;

public class OrganizationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object> Settings { get; set; } = new();

    // Navigation properties
    public UserEntity Owner { get; set; } = null!;
    public ICollection<OrganizationMemberEntity> Members { get; set; } = new List<OrganizationMemberEntity>();
    public ICollection<RoleEntity> Roles { get; set; } = new List<RoleEntity>();
    public ICollection<UrlEntity> Urls { get; set; } = new List<UrlEntity>();
}
