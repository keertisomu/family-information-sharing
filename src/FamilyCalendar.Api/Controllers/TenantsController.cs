using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FamilyCalendar.Api.Data;
using FamilyCalendar.Api.Models;

namespace FamilyCalendar.Api.Controllers;

/// <summary>
/// Tenant management endpoints for creating, updating, and managing family groups
/// </summary>
[ApiController]
[Route("api/v1/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ILogger<TenantsController> _logger;
    private readonly ApplicationDbContext _context;

    public TenantsController(
        ILogger<TenantsController> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Creates a new tenant (family group) - requires global admin privileges
    /// </summary>
    /// <param name="request">Tenant creation request with name and owner email</param>
    /// <returns>Created tenant details</returns>
    [HttpPost]
    public async Task<ActionResult<TenantResponse>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null || !user.IsGlobalAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to create tenant without global admin privileges", userId.Value);
            return Forbid();
        }

        // Find owner by email
        var owner = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.OwnerEmail);
        if (owner == null)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = $"User with email {request.OwnerEmail} not found" });
        }

        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = request.Name,
            OwnerId = owner.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tenants.Add(tenant);

        var ownerMembership = new TenantMember
        {
            TenantMemberId = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            UserId = owner.UserId,
            Role = TenantRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        _context.TenantMembers.Add(ownerMembership);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} created by global admin {UserId}", tenant.TenantId, userId.Value);

        return CreatedAtAction(nameof(GetTenant), new { id = tenant.TenantId }, new TenantResponse
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Description = null,
            OwnerId = tenant.OwnerId,
            OwnerName = owner.Name,
            OwnerEmail = owner.Email,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        });
    }

    /// <summary>
    /// Lists all tenants accessible to the authenticated user with pagination
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20)</param>
    /// <returns>Paginated list of tenants</returns>
    [HttpGet]
    public async Task<ActionResult<TenantListResponse>> ListTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var isGlobalAdmin = await IsGlobalAdmin(userId.Value);

        IQueryable<Tenant> query;
        if (isGlobalAdmin)
        {
            query = _context.Tenants;
        }
        else
        {
            var userTenantIds = await _context.TenantMembers
                .Where(tm => tm.UserId == userId.Value)
                .Select(tm => tm.TenantId)
                .ToListAsync();
            query = _context.Tenants.Where(t => userTenantIds.Contains(t.TenantId));
        }

        var total = await query.CountAsync();
        var tenants = await query
            .Include(t => t.Owner)
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TenantResponse
            {
                TenantId = t.TenantId,
                Name = t.Name,
                Description = null,
                OwnerId = t.OwnerId,
                OwnerName = t.Owner.Name,
                OwnerEmail = t.Owner.Email,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(new TenantListResponse
        {
            Data = tenants,
            Meta = new PaginationMeta
            {
                Page = page,
                Limit = pageSize,
                Total = total
            }
        });
    }

    /// <summary>
    /// Retrieves details of a specific tenant by ID
    /// </summary>
    /// <param name="id">Tenant unique identifier</param>
    /// <returns>Tenant details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenantResponse>> GetTenant(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var isGlobalAdmin = await IsGlobalAdmin(userId.Value);
        var isMember = await _context.TenantMembers.AnyAsync(tm => tm.TenantId == id && tm.UserId == userId.Value);

        if (!isGlobalAdmin && !isMember)
        {
            _logger.LogWarning("User {UserId} attempted to access tenant {TenantId} without authorization", userId.Value, id);
            return Forbid();
        }

        var tenant = await _context.Tenants
            .Include(t => t.Owner)
            .FirstOrDefaultAsync(t => t.TenantId == id);
            
        if (tenant == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = $"Tenant {id} not found" });
        }

        return Ok(new TenantResponse
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Description = null,
            OwnerId = tenant.OwnerId,
            OwnerName = tenant.Owner.Name,
            OwnerEmail = tenant.Owner.Email,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        });
    }

    /// <summary>
    /// Updates a tenant's properties (requires owner or global admin privileges)
    /// </summary>
    /// <param name="id">Tenant unique identifier</param>
    /// <param name="request">Update request with optional name and description</param>
    /// <returns>Updated tenant details</returns>
    [HttpPatch("{id}")]
    public async Task<ActionResult<TenantResponse>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var tenant = await _context.Tenants.FindAsync(id);
        if (tenant == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = $"Tenant {id} not found" });
        }

        var isGlobalAdmin = await IsGlobalAdmin(userId.Value);
        if (!isGlobalAdmin && tenant.OwnerId != userId.Value)
        {
            _logger.LogWarning("User {UserId} attempted to update tenant {TenantId} without authorization", userId.Value, id);
            return Forbid();
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            tenant.Name = request.Name;
        }

        tenant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} updated by user {UserId}", id, userId.Value);

        // Reload with owner for response
        await _context.Entry(tenant).Reference(t => t.Owner).LoadAsync();

        return Ok(new TenantResponse
        {
            TenantId = tenant.TenantId,
            Name = tenant.Name,
            Description = null,
            OwnerId = tenant.OwnerId,
            OwnerName = tenant.Owner.Name,
            OwnerEmail = tenant.Owner.Email,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        });
    }

    /// <summary>
    /// Deletes a tenant (requires global admin privileges)
    /// </summary>
    /// <param name="id">Tenant unique identifier</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTenant(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var isGlobalAdmin = await IsGlobalAdmin(userId.Value);
        if (!isGlobalAdmin)
        {
            _logger.LogWarning("User {UserId} attempted to delete tenant {TenantId} without global admin privileges", userId.Value, id);
            return Forbid();
        }

        var tenant = await _context.Tenants.FindAsync(id);
        if (tenant == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = $"Tenant {id} not found" });
        }

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tenant {TenantId} deleted by global admin {UserId}", id, userId.Value);
        return NoContent();
    }

    /// <summary>
    /// Lists all members of a specific tenant
    /// </summary>
    /// <param name="id">Tenant unique identifier</param>
    /// <returns>List of tenant members</returns>
    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<TenantMemberResponse>>> ListTenantMembers(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var isGlobalAdmin = await IsGlobalAdmin(userId.Value);
        var isMember = await _context.TenantMembers.AnyAsync(tm => tm.TenantId == id && tm.UserId == userId.Value);

        if (!isGlobalAdmin && !isMember)
        {
            _logger.LogWarning("User {UserId} attempted to list members of tenant {TenantId} without authorization", userId.Value, id);
            return Forbid();
        }

        var members = await _context.TenantMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TenantId == id)
            .Select(tm => new TenantMemberResponse
            {
                UserId = tm.UserId,
                Email = tm.User.Email,
                Name = tm.User.Name,
                Role = tm.Role.ToString().ToLower(),
                JoinedAt = tm.JoinedAt
            })
            .ToListAsync();

        return Ok(members);
    }

    /// <summary>
    /// Removes a member from a tenant (requires owner or global admin privileges)
    /// </summary>
    /// <param name="tenantId">Tenant unique identifier</param>
    /// <param name="userId">User ID of member to remove</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{tenantId}/members")]
    public async Task<IActionResult> RemoveTenantMember(Guid tenantId, [FromQuery] Guid userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized(new ErrorResponse { StatusCode = 401, Error = "Unauthorized", Message = "User not authenticated" });
        }

        var tenant = await _context.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = $"Tenant {tenantId} not found" });
        }

        // Only owner can remove members (not global admin per contract)
        if (tenant.OwnerId != currentUserId.Value)
        {
            _logger.LogWarning("User {UserId} attempted to remove member from tenant {TenantId} without owner privileges", currentUserId.Value, tenantId);
            return StatusCode(403, new ErrorResponse { StatusCode = 403, Error = "Forbidden", Message = "Only tenant owner can remove members" });
        }

        // Cannot remove the owner themselves
        if (userId == tenant.OwnerId)
        {
            return BadRequest(new ErrorResponse { StatusCode = 400, Error = "Bad Request", Message = "Cannot remove the tenant owner" });
        }

        // Find and remove membership
        var membership = await _context.TenantMembers
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.UserId == userId);
        
        if (membership == null)
        {
            return NotFound(new ErrorResponse { StatusCode = 404, Error = "Not Found", Message = $"User {userId} is not a member of tenant {tenantId}" });
        }

        _context.TenantMembers.Remove(membership);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} removed from tenant {TenantId} by owner {CurrentUserId}", userId, tenantId, currentUserId.Value);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }
        return userId;
    }

    private async Task<bool> IsGlobalAdmin(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.IsGlobalAdmin ?? false;
    }
}
