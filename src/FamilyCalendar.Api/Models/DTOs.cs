namespace FamilyCalendar.Api.Models;

/// <summary>
/// DTO classes for authentication API responses and requests
/// </summary>
/// 
public class AuthResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required UserInfo User { get; set; }
    public TenantInfo? Tenant { get; set; }
}

public class UserInfo
{
    public Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public bool IsGlobalAdmin { get; set; }
}

public class TenantInfo
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string Role { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public class SelectTenantRequest
{
    public Guid TenantId { get; set; }
}

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public required string Error { get; set; }
    public required string Message { get; set; }
}

/// <summary>
/// DTO classes for tenant API requests and responses
/// </summary>

public class CreateTenantRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string OwnerEmail { get; set; }
}

public class UpdateTenantRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class TenantResponse
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public required string OwnerName { get; set; }
    public required string OwnerEmail { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TenantListResponse
{
    public required List<TenantResponse> Data { get; set; }
    public required PaginationMeta Meta { get; set; }
}

public class PaginationMeta
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
}

public class TenantMemberResponse
{
    public Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class TenantMembersListResponse
{
    public required List<TenantMemberResponse> Data { get; set; }
}

/// <summary>
/// DTO classes for invitation API requests and responses
/// </summary>

public class CreateInvitationRequest
{
    public required string InvitedEmail { get; set; }
}

public class InvitationResponse
{
    public Guid InvitationId { get; set; }
    public Guid TenantId { get; set; }
    public required string TenantName { get; set; }
    public required string InvitedEmail { get; set; }
    public required InviterInfo InvitedBy { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class InviterInfo
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public class InvitationListResponse
{
    public required List<InvitationResponse> Data { get; set; }
}

public class AcceptInvitationResponse
{
    public required string Message { get; set; }
    public Guid TenantId { get; set; }
}
/// <summary>
/// DTO classes for event API requests and responses
/// </summary>

public class CreateEventRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Location { get; set; }
    public Guid? AssignedTo { get; set; }
}

public class UpdateEventRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
    public Guid? AssignedTo { get; set; }
}

public class EventResponse
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Location { get; set; }
    public UserDetails? AssignedTo { get; set; }
    public required UserDetails CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserDetails
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public class EventListResponse
{
    public required List<EventResponse> Data { get; set; }
    public required PaginationMeta Meta { get; set; }
}