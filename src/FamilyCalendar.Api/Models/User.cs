namespace FamilyCalendar.Api.Models;

/// <summary>
/// Represents an authenticated user via Google OAuth.
/// Can be a global admin, tenant owner, or tenant member.
/// </summary>
public class User
{
    public Guid UserId { get; set; }
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsGlobalAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Tenant> OwnedTenants { get; set; } = new List<Tenant>();
    public ICollection<TenantMember> Memberships { get; set; } = new List<TenantMember>();
    public ICollection<CalendarEvent> CreatedEvents { get; set; } = new List<CalendarEvent>();
    public ICollection<CalendarEvent> AssignedEvents { get; set; } = new List<CalendarEvent>();
}
