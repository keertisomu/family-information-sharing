using System.Security.Claims;

namespace FamilyCalendar.Api.Services;

/// <summary>
/// Implementation of ITenantProvider that extracts tenant context from HTTP request claims.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetTenantId()
    {
        // During migrations or background operations, no HTTP context exists
        // Return a sentinel value that will make no query match (all tenants will be filtered out)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null || httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Guid.Empty; // Safe default for non-HTTP or unauthenticated contexts
        }
        
        var claim = httpContext.User.FindFirst("tenant_id");
        if (claim == null || !Guid.TryParse(claim.Value, out var tenantId))
        {
            // User is authenticated but not part of any tenant yet (e.g., accepting initial invitation)
            // Return Guid.Empty to filter out all tenants from unfiltered queries
            return Guid.Empty;
        }
        return tenantId;
    }

    public Guid GetUserId()
    {
        // During migrations or background operations, no HTTP context exists
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null || httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Guid.Empty; // Safe default for non-HTTP or unauthenticated contexts
        }
        
        var claim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }
        return userId;
    }

    public bool IsGlobalAdmin()
    {
        // During migrations or background operations, no HTTP context exists
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null || httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return true; // Allow all queries during migrations or unauthenticated requests
        }
        
        var claim = httpContext.User.FindFirst("is_global_admin");
        return bool.TryParse(claim?.Value, out var result) && result;
    }
}
