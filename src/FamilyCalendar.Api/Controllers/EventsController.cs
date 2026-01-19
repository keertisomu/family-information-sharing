using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FamilyCalendar.Api.Data;
using FamilyCalendar.Api.Models;

namespace FamilyCalendar.Api.Controllers;

/// <summary>
/// Calendar event management endpoints for creating, viewing, updating, and deleting events
/// </summary>
[ApiController]
[Route("api/v1/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly ILogger<EventsController> _logger;
    private readonly ApplicationDbContext _context;

    public EventsController(
        ILogger<EventsController> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Creates a new calendar event for the current tenant
    /// </summary>
    /// <param name="request">Event creation request with title, times, and optional details</param>
    /// <returns>Created event details</returns>
    /// <remarks>
    /// Validates:
    /// - start_time must be before end_time
    /// - assigned_to (if provided) must be a tenant member
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<EventResponse>> CreateEvent([FromBody] CreateEventRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "Tenant not selected" });
        }

        // Validate start_time < end_time
        if (request.StartTime >= request.EndTime)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "start_time must be before end_time" });
        }

        // Validate assigned_to if provided
        if (request.AssignedTo.HasValue)
        {
            var isMember = await _context.TenantMembers
                .AnyAsync(tm => tm.TenantId == tenantId.Value && tm.UserId == request.AssignedTo.Value);

            if (!isMember)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "assigned_to user is not a member of this tenant" });
            }
        }

        var @event = new CalendarEvent
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId.Value,
            CreatorId = userId.Value,
            AssignedTo = request.AssignedTo,
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CalendarEvents.Add(@event);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} created by user {UserId} in tenant {TenantId}", 
            @event.EventId, userId.Value, tenantId.Value);

        return CreatedAtAction(nameof(GetEvent), new { eventId = @event.EventId }, 
            await BuildEventResponse(@event));
    }

    /// <summary>
    /// Retrieves a single calendar event by ID (tenant-scoped)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Event details</returns>
    [HttpGet("{eventId}")]
    public async Task<ActionResult<EventResponse>> GetEvent(Guid eventId)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "Tenant not selected" });
        }

        var @event = await _context.CalendarEvents
            .Include(e => e.Creator)
            .Include(e => e.Assignee)
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.TenantId == tenantId.Value);

        if (@event == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Event not found" });
        }

        return Ok(await BuildEventResponse(@event));
    }

    /// <summary>
    /// Updates an existing calendar event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="request">Event update request with partial fields</param>
    /// <returns>Updated event details</returns>
    /// <remarks>
    /// Validates:
    /// - start_time must be before end_time (if both provided)
    /// - assigned_to (if provided) must be a tenant member
    /// </remarks>
    [HttpPut("{eventId}")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(Guid eventId, [FromBody] UpdateEventRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "Tenant not selected" });
        }

        var @event = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.TenantId == tenantId.Value);

        if (@event == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Event not found" });
        }

        // Validate times if both provided
        var startTime = request.StartTime ?? @event.StartTime;
        var endTime = request.EndTime ?? @event.EndTime;

        if (startTime >= endTime)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "start_time must be before end_time" });
        }

        // Validate assigned_to if provided
        if (request.AssignedTo.HasValue)
        {
            var isMember = await _context.TenantMembers
                .AnyAsync(tm => tm.TenantId == tenantId.Value && tm.UserId == request.AssignedTo.Value);

            if (!isMember)
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "assigned_to user is not a member of this tenant" });
            }
        }

        // Update fields
        @event.Title = request.Title ?? @event.Title;
        @event.Description = request.Description ?? @event.Description;
        @event.StartTime = startTime;
        @event.EndTime = endTime;
        @event.AssignedTo = request.AssignedTo ?? @event.AssignedTo;
        @event.UpdatedAt = DateTime.UtcNow;

        _context.CalendarEvents.Update(@event);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} updated by user {UserId} in tenant {TenantId}", 
            eventId, userId.Value, tenantId.Value);

        await _context.Entry(@event).ReloadAsync();
        await _context.Entry(@event).Reference(e => e.Creator).LoadAsync();
        if (@event.AssignedTo.HasValue)
        {
            await _context.Entry(@event).Reference(e => e.Assignee).LoadAsync();
        }

        return Ok(await BuildEventResponse(@event));
    }

    /// <summary>
    /// Deletes a calendar event permanently
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{eventId}")]
    public async Task<IActionResult> DeleteEvent(Guid eventId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var tenantId = GetCurrentTenantId();
        if (tenantId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "Tenant not selected" });
        }

        var @event = await _context.CalendarEvents
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.TenantId == tenantId.Value);

        if (@event == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Event not found" });
        }

        _context.CalendarEvents.Remove(@event);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Event {EventId} deleted by user {UserId} in tenant {TenantId}", 
            eventId, userId.Value, tenantId.Value);

        return NoContent();
    }

    /// <summary>
    /// Helper method to build EventResponse from CalendarEvent entity
    /// </summary>
    private async Task<EventResponse> BuildEventResponse(CalendarEvent @event)
    {
        // Ensure relationships are loaded
        if (@event.Creator == null)
        {
            await _context.Entry(@event).Reference(e => e.Creator).LoadAsync();
        }

        if (@event.AssignedTo.HasValue && @event.Assignee == null)
        {
            await _context.Entry(@event).Reference(e => e.Assignee).LoadAsync();
        }

        return new EventResponse
        {
            EventId = @event.EventId,
            TenantId = @event.TenantId,
            Title = @event.Title,
            Description = @event.Description,
            StartTime = @event.StartTime,
            EndTime = @event.EndTime,
            AssignedTo = @event.Assignee != null ? new UserDetails
            {
                UserId = @event.Assignee.UserId,
                Name = @event.Assignee.Name,
                Email = @event.Assignee.Email
            } : null,
            CreatedBy = new UserDetails
            {
                UserId = @event.Creator!.UserId,
                Name = @event.Creator.Name,
                Email = @event.Creator.Email
            },
            CreatedAt = @event.CreatedAt,
            UpdatedAt = @event.UpdatedAt
        };
    }

    /// <summary>
    /// Extracts user ID from JWT claims
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }

    /// <summary>
    /// Extracts tenant ID from JWT claims
    /// </summary>
    private Guid? GetCurrentTenantId()
    {
        var tenantIdClaim = User.FindFirst("tenant_id");
        return tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId) ? tenantId : null;
    }
}
