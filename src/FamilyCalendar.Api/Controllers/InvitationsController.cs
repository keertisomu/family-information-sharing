using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FamilyCalendar.Api.Data;
using FamilyCalendar.Api.Models;
using FamilyCalendar.Api.Services;

namespace FamilyCalendar.Api.Controllers;

/// <summary>
/// Invitation management endpoints for inviting family members to tenants
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly ILogger<InvitationsController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IEmailService _emailService;

    public InvitationsController(
        ILogger<InvitationsController> logger,
        ApplicationDbContext context,
        ITenantProvider tenantProvider,
        IEmailService emailService)
    {
        _logger = logger;
        _context = context;
        _tenantProvider = tenantProvider;
        _emailService = emailService;
    }

    /// <summary>
    /// Helper method to get the current user's ID from JWT claims
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    /// <summary>
    /// Helper method to check if user is owner of a tenant
    /// </summary>
    private async Task<bool> IsOwnerOfTenant(Guid userId, Guid tenantId)
    {
        return await _context.TenantMembers
            .AnyAsync(tm => tm.UserId == userId && tm.TenantId == tenantId && tm.Role == TenantRole.Owner);
    }

    /// <summary>
    /// Creates a new invitation to join a tenant (owner only)
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="request">Invitation request with invited email</param>
    /// <returns>Created invitation details</returns>
    [HttpPost("tenants/{tenantId}/invitations")]
    public async Task<ActionResult<InvitationResponse>> CreateInvitation(
        Guid tenantId,
        [FromBody] CreateInvitationRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        // Validate email format
        if (!new EmailAddressAttribute().IsValid(request.InvitedEmail))
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "Invalid email format" });
        }

        // Check if user is owner of tenant
        if (!await IsOwnerOfTenant(userId.Value, tenantId))
        {
            _logger.LogWarning("User {UserId} attempted to create invitation for tenant {TenantId} without owner privileges", userId.Value, tenantId);
            return StatusCode(403, new ErrorResponse { StatusCode = 403, Error = "Forbidden", Message = "Only tenant owners can create invitations" });
        }

        // Get tenant details
        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Tenant not found" });
        }

        // Check if invited email is already a member
        var existingMember = await _context.TenantMembers
            .Include(tm => tm.User)
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.User.Email == request.InvitedEmail);
        
        if (existingMember != null)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "User is already a member of this tenant" });
        }

        // Check for duplicate pending invitation
        var existingPendingInvitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.TenantId == tenantId 
                && i.InvitedEmail == request.InvitedEmail 
                && i.Status == InvitationStatus.Pending);
        
        if (existingPendingInvitation != null)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "User already has a pending invitation" });
        }

        // Create invitation
        var now = DateTime.UtcNow;
        var invitation = new Invitation
        {
            InvitationId = Guid.NewGuid(),
            TenantId = tenantId,
            InvitedEmail = request.InvitedEmail,
            InviterId = userId.Value,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        _context.Invitations.Add(invitation);
        await _context.SaveChangesAsync();

        // Get inviter details for response and email
        var inviter = await _context.Users.FindAsync(userId.Value);
        if (inviter == null)
        {
            return StatusCode(500, new ErrorResponse { StatusCode = 500, Error = "Internal Server Error", Message = "Failed to retrieve inviter details" });
        }

        // Send email notification
        try
        {
            var acceptUrl = $"{Request.Scheme}://{Request.Host}/api/v1/invitations/{invitation.InvitationId}/accept";
            await _emailService.SendInvitationEmailAsync(
                request.InvitedEmail,
                tenant.Name,
                inviter.Name,
                acceptUrl
            );

            _logger.LogInformation("Invitation {InvitationId} created for tenant {TenantId} by user {UserId}, email sent to {InvitedEmail}",
                invitation.InvitationId, tenantId, userId.Value, request.InvitedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invitation email for invitation {InvitationId}", invitation.InvitationId);
            // Continue - invitation created successfully even if email fails
        }

        var response = new InvitationResponse
        {
            InvitationId = invitation.InvitationId,
            TenantId = tenant.TenantId,
            TenantName = tenant.Name,
            InvitedEmail = invitation.InvitedEmail,
            InvitedBy = new InviterInfo
            {
                UserId = inviter.UserId,
                Name = inviter.Name,
                Email = inviter.Email
            },
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt
        };

        return CreatedAtAction(
            nameof(GetInvitation),
            new { invitationId = invitation.InvitationId },
            response);
    }

    /// <summary>
    /// Gets a specific invitation by ID
    /// </summary>
    /// <param name="invitationId">The invitation ID</param>
    /// <returns>Invitation details</returns>
    [HttpGet("invitations/{invitationId}")]
    public async Task<ActionResult<InvitationResponse>> GetInvitation(Guid invitationId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var invitation = await _context.Invitations
            .Include(i => i.Tenant)
            .Include(i => i.Inviter)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Invitation not found" });
        }

        // Check if user is the inviter or invited user
        var isInviter = invitation.InviterId == userId.Value;
        var isInvited = invitation.InvitedEmail == User.FindFirst(ClaimTypes.Email)?.Value;
        
        if (!isInviter && !isInvited)
        {
            return StatusCode(403, new ErrorResponse { StatusCode = 403, Error = "Forbidden", Message = "You don't have permission to view this invitation" });
        }

        return Ok(new InvitationResponse
        {
            InvitationId = invitation.InvitationId,
            TenantId = invitation.TenantId,
            TenantName = invitation.Tenant.Name,
            InvitedEmail = invitation.InvitedEmail,
            InvitedBy = new InviterInfo
            {
                UserId = invitation.Inviter.UserId,
                Name = invitation.Inviter.Name,
                Email = invitation.Inviter.Email
            },
            Status = invitation.Status.ToString(),
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt
        });
    }

    /// <summary>
    /// Lists all invitations for a tenant (owner only)
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of invitations</returns>
    [HttpGet("tenants/{tenantId}/invitations")]
    public async Task<ActionResult<InvitationListResponse>> ListTenantInvitations(
        Guid tenantId,
        [FromQuery] string? status = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        // Check if user is owner of tenant
        if (!await IsOwnerOfTenant(userId.Value, tenantId))
        {
            _logger.LogWarning("User {UserId} attempted to list invitations for tenant {TenantId} without owner privileges", userId.Value, tenantId);
            return StatusCode(403, new ErrorResponse { StatusCode = 403, Error = "Forbidden", Message = "Only tenant owners can view invitations" });
        }

        // Parse status filter if provided
        InvitationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (Enum.TryParse<InvitationStatus>(status, true, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }
            else
            {
                return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = $"Invalid status value: {status}" });
            }
        }

        // Query invitations
        var query = _context.Invitations
            .Include(i => i.Inviter)
            .Include(i => i.Tenant)
            .Where(i => i.TenantId == tenantId);

        if (statusFilter.HasValue)
        {
            query = query.Where(i => i.Status == statusFilter.Value);
        }

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var response = new InvitationListResponse
        {
            Data = invitations.Select(i => new InvitationResponse
            {
                InvitationId = i.InvitationId,
                TenantId = i.TenantId,
                TenantName = i.Tenant.Name,
                InvitedEmail = i.InvitedEmail,
                InvitedBy = new InviterInfo
                {
                    UserId = i.Inviter.UserId,
                    Name = i.Inviter.Name,
                    Email = i.Inviter.Email
                },
                Status = i.Status.ToString(),
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Lists user's pending invitations
    /// </summary>
    /// <returns>List of pending invitations for the authenticated user</returns>
    [HttpGet("invitations")]
    public async Task<ActionResult<InvitationListResponse>> ListMyInvitations()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        // Get user's email
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not found" });
        }

        var now = DateTime.UtcNow;

        // Query pending, non-expired invitations for user's email
        var invitations = await _context.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Inviter)
            .Include(i => i.Tenant)
            .Where(i => i.InvitedEmail == user.Email
                && i.Status == InvitationStatus.Pending
                && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var response = new InvitationListResponse
        {
            Data = invitations.Select(i => new InvitationResponse
            {
                InvitationId = i.InvitationId,
                TenantId = i.TenantId,
                TenantName = i.Tenant.Name,
                InvitedEmail = i.InvitedEmail,
                InvitedBy = new InviterInfo
                {
                    UserId = i.Inviter.UserId,
                    Name = i.Inviter.Name,
                    Email = i.Inviter.Email
                },
                Status = i.Status.ToString(),
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Accepts an invitation to join a tenant
    /// </summary>
    /// <param name="invitationId">The invitation ID</param>
    /// <returns>Success message with tenant ID</returns>
    [HttpPost("invitations/{invitationId}/accept")]
    public async Task<ActionResult<AcceptInvitationResponse>> AcceptInvitation(Guid invitationId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        // Get user details
        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not found" });
        }

        // Get invitation
        var invitation = await _context.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Invitation not found" });
        }

        // Validate invitation status
        if (invitation.Status != InvitationStatus.Pending)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = $"Invitation has already been {invitation.Status.ToString().ToLower()}" });
        }

        // Validate email match
        if (invitation.InvitedEmail != user.Email)
        {
            _logger.LogWarning("User {UserId} attempted to accept invitation {InvitationId} sent to different email", userId.Value, invitationId);
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "This invitation was sent to a different email" });
        }

        // Check if invitation expired
        var now = DateTime.UtcNow;
        if (invitation.ExpiresAt <= now)
        {
            invitation.Status = InvitationStatus.Expired;
            await _context.SaveChangesAsync();
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "Invitation expired (valid for 24 hours only)" });
        }

        // Check if user is already a member
        var existingMembership = await _context.TenantMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tm => tm.TenantId == invitation.TenantId && tm.UserId == userId.Value);

        if (existingMembership != null)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "Already a member of this tenant" });
        }

        // Create tenant membership
        var membership = new TenantMember
        {
            TenantMemberId = Guid.NewGuid(),
            TenantId = invitation.TenantId,
            UserId = userId.Value,
            Role = TenantRole.Member,
            JoinedAt = now
        };

        _context.TenantMembers.Add(membership);

        // Update invitation status
        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = now;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} accepted invitation {InvitationId} and joined tenant {TenantId}",
            userId.Value, invitationId, invitation.TenantId);

        return Ok(new AcceptInvitationResponse
        {
            Message = $"Successfully joined {invitation.Tenant.Name}",
            TenantId = invitation.TenantId
        });
    }

    /// <summary>
    /// Revokes a pending invitation (owner only)
    /// </summary>
    /// <param name="invitationId">The invitation ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("invitations/{invitationId}")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        // Get invitation
        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.InvitationId == invitationId);

        if (invitation == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = "Invitation not found" });
        }

        // Check if user is owner of tenant
        if (!await IsOwnerOfTenant(userId.Value, invitation.TenantId))
        {
            _logger.LogWarning("User {UserId} attempted to revoke invitation {InvitationId} without owner privileges", userId.Value, invitationId);
            return StatusCode(403, new ErrorResponse { StatusCode = 403, Error = "Forbidden", Message = "Only tenant owners can revoke invitations" });
        }

        // Check if invitation can be revoked
        if (invitation.Status != InvitationStatus.Pending)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = $"Cannot revoke invitation that is already {invitation.Status.ToString().ToLower()}" });
        }

        // Revoke invitation
        invitation.Status = InvitationStatus.Revoked;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} revoked invitation {InvitationId} for tenant {TenantId}",
            userId.Value, invitationId, invitation.TenantId);

        return NoContent();
    }
}
