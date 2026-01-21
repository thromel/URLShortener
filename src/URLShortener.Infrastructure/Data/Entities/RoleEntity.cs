namespace URLShortener.Infrastructure.Data.Entities;

public class RoleEntity
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }  // Null for system-wide roles
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }  // System roles cannot be deleted
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public OrganizationEntity? Organization { get; set; }
    public ICollection<OrganizationMemberEntity> Members { get; set; } = new List<OrganizationMemberEntity>();
}

public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
}

public static class Permissions
{
    // URL Operations
    public const string UrlCreate = "url:create";
    public const string UrlRead = "url:read";
    public const string UrlUpdate = "url:update";
    public const string UrlDelete = "url:delete";

    // Analytics
    public const string AnalyticsView = "analytics:view";
    public const string AnalyticsExport = "analytics:export";

    // Member Management
    public const string MembersView = "members:view";
    public const string MembersInvite = "members:invite";
    public const string MembersRemove = "members:remove";
    public const string MembersManageRoles = "members:manage-roles";

    // Organization
    public const string OrgSettings = "org:settings";
    public const string OrgBilling = "org:billing";
    public const string OrgDelete = "org:delete";

    public static readonly string[] All = new[]
    {
        UrlCreate, UrlRead, UrlUpdate, UrlDelete,
        AnalyticsView, AnalyticsExport,
        MembersView, MembersInvite, MembersRemove, MembersManageRoles,
        OrgSettings, OrgBilling, OrgDelete
    };

    public static readonly string[] OwnerPermissions = All;

    public static readonly string[] AdminPermissions = new[]
    {
        UrlCreate, UrlRead, UrlUpdate, UrlDelete,
        AnalyticsView, AnalyticsExport,
        MembersView, MembersInvite, MembersRemove, MembersManageRoles,
        OrgSettings, OrgBilling
    };

    public static readonly string[] MemberPermissions = new[]
    {
        UrlCreate, UrlRead, UrlUpdate,
        AnalyticsView
    };
}
