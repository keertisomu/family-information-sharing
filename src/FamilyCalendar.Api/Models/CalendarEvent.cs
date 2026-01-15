namespace FamilyCalendar.Api.Models;

/// <summary>
/// Represents a calendar event within a tenant's calendar.
/// </summary>
public class CalendarEvent
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? AssignedTo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public User? Assignee { get; set; }
}
