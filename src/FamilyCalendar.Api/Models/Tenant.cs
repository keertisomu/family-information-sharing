namespace FamilyCalendar.Api.Models;

/// <summary>
/// Represents a family or group calendar workspace with complete data isolation.
/// </summary>
public class Tenant
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User Owner { get; set; } = null!;
    public ICollection<TenantMember> Members { get; set; } = new List<TenantMember>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
}
