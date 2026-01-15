namespace FamilyCalendar.Api.Models;

/// <summary>
/// Represents the many-to-many relationship between users and tenants with role information.
/// </summary>
public class TenantMember
{
    public Guid TenantMemberId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public TenantRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>
/// Defines the role a user has within a tenant.
/// </summary>
public enum TenantRole
{
    Owner,
    Member
}
