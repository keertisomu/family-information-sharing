namespace FamilyCalendar.Api.Services;

/// <summary>
/// Provides access to the current tenant context and user information from the HTTP request.
/// Used by ApplicationDbContext for multi-tenant data isolation through global query filters.
/// </summary>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the tenant ID from the current user's JWT claims.
    /// </summary>
    /// <returns>The tenant ID.</returns>
    /// <exception cref="UnauthorizedAccessException">If the tenant_id claim is not present.</exception>
    Guid GetTenantId();

    /// <summary>
    /// Gets the user ID from the current user's JWT claims.
    /// </summary>
    /// <returns>The user ID.</returns>
    /// <exception cref="UnauthorizedAccessException">If the user is not authenticated.</exception>
    Guid GetUserId();

    /// <summary>
    /// Determines whether the current user is a global administrator.
    /// </summary>
    /// <returns>True if the user has the is_global_admin claim set to true, otherwise false.</returns>
    bool IsGlobalAdmin();
}
