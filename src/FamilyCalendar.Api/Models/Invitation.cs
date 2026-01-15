namespace FamilyCalendar.Api.Models;

/// <summary>
/// Represents pending, accepted, expired, or revoked invitations to join a tenant.
/// </summary>
public class Invitation
{
    public Guid InvitationId { get; set; }
    public Guid TenantId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public Guid InviterId { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User Inviter { get; set; } = null!;
}

/// <summary>
/// Defines the status of an invitation.
/// </summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Revoked
}
