namespace URLShortener.Infrastructure.Data.Entities;

public class OrganizationMemberEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime JoinedAt { get; set; }
    public Guid? InvitedById { get; set; }

    // Navigation properties
    public OrganizationEntity Organization { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
    public RoleEntity Role { get; set; } = null!;
}
