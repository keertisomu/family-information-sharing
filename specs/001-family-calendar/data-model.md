# Data Model: Multi-Tenant Family Calendar System

**Feature**: [spec.md](./spec.md)  
**Phase**: 1 - Design & Contracts  
**Date**: 2025-12-06

## Entity Relationship Diagram

```
┌──────────────────┐
│      User        │
├──────────────────┤
│ UserId (PK)      │◄────────┐
│ GoogleId (UK)    │         │
│ Email            │         │
│ Name             │         │
│ IsGlobalAdmin    │         │
│ CreatedAt        │         │
│ UpdatedAt        │         │
└──────────────────┘         │
         │                   │
         │ Owns              │ Invites
         ▼                   │
┌──────────────────┐         │
│     Tenant       │         │
├──────────────────┤         │
│ TenantId (PK)    │◄────┐   │
│ Name             │     │   │
│ Description      │     │   │
│ OwnerId (FK)     │─────┘   │
│ CreatedAt        │         │
│ UpdatedAt        │         │
└──────────────────┘         │
         │                   │
         │ Has Members       │
         ▼                   │
┌──────────────────┐         │
│  TenantMember    │         │
├──────────────────┤         │
│ TenantMemberId(PK)│        │
│ TenantId (FK)    │─────┐   │
│ UserId (FK)      │─────┼───┘
│ Role             │     │
│ JoinedAt         │     │
└──────────────────┘     │
                         │
┌──────────────────┐     │
│   Invitation     │     │
├──────────────────┤     │
│ InvitationId (PK)│     │
│ TenantId (FK)    │─────┤
│ InvitedEmail     │     │
│ InviterId (FK)   │     │
│ Status           │     │
│ CreatedAt        │     │
│ ExpiresAt        │     │
│ AcceptedAt       │     │
└──────────────────┘     │
                         │
┌──────────────────┐     │
│  CalendarEvent   │     │
├──────────────────┤     │
│ EventId (PK)     │     │
│ TenantId (FK)    │─────┘
│ CreatorId (FK)   │────┐
│ AssignedTo (FK)  │────┤
│ Title            │    │
│ Description      │    │
│ StartTime        │    │
│ EndTime          │    │
│ CreatedAt        │    │
│ UpdatedAt        │    │
└──────────────────┘    │
         │              │
         └──────────────┘
         (Both FK to User)
```

## Entity Definitions

### 1. User

Represents an authenticated user via Google OAuth. Can be a global admin, tenant owner, or tenant member.

**Table**: `users`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| user_id | UUID | PRIMARY KEY | Unique identifier |
| google_id | VARCHAR(255) | UNIQUE NOT NULL | Google user ID from OAuth |
| email | VARCHAR(255) | NOT NULL | User's email from Google |
| name | VARCHAR(255) | NOT NULL | Display name from Google |
| is_global_admin | BOOLEAN | NOT NULL DEFAULT false | System-level admin flag |
| created_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Last update time |

**Indexes**:
- PRIMARY KEY on `user_id`
- UNIQUE INDEX on `google_id`
- INDEX on `email`

**EF Core Entity**:
```csharp
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
```

---

### 2. Tenant

Represents a family or group calendar workspace with complete data isolation.

**Table**: `tenants`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_id | UUID | PRIMARY KEY | Unique identifier |
| name | VARCHAR(255) | NOT NULL | Tenant display name (e.g., "Smith Family") |
| description | TEXT | NULL | Optional description |
| owner_id | UUID | FOREIGN KEY (users.user_id) NOT NULL | Designated owner |
| created_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Last update time |

**Indexes**:
- PRIMARY KEY on `tenant_id`
- INDEX on `owner_id`

**EF Core Entity**:
```csharp
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
```

---

### 3. TenantMember

Represents the many-to-many relationship between users and tenants with role information.

**Table**: `tenant_members`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| tenant_member_id | UUID | PRIMARY KEY | Unique identifier |
| tenant_id | UUID | FOREIGN KEY (tenants.tenant_id) NOT NULL | Reference to tenant |
| user_id | UUID | FOREIGN KEY (users.user_id) NOT NULL | Reference to user |
| role | VARCHAR(50) | NOT NULL CHECK IN ('Owner', 'Member') | Role within tenant |
| joined_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Membership start time |

**Indexes**:
- PRIMARY KEY on `tenant_member_id`
- UNIQUE INDEX on `(tenant_id, user_id)` - user can only be member once per tenant
- INDEX on `tenant_id`
- INDEX on `user_id`

**EF Core Entity**:
```csharp
public class TenantMember
{
    public Guid TenantMemberId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public TenantRole Role { get; set; }
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum TenantRole
{
    Owner,
    Member
}
```

---

### 4. Invitation

Represents pending, accepted, expired, or revoked invitations to join a tenant.

**Table**: `invitations`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| invitation_id | UUID | PRIMARY KEY | Unique identifier |
| tenant_id | UUID | FOREIGN KEY (tenants.tenant_id) NOT NULL | Target tenant |
| invited_email | VARCHAR(255) | NOT NULL | Invitee's email address |
| inviter_id | UUID | FOREIGN KEY (users.user_id) NOT NULL | User who sent invite |
| status | VARCHAR(50) | NOT NULL CHECK IN ('Pending', 'Accepted', 'Expired', 'Revoked') | Invitation state |
| created_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Invitation sent time |
| expires_at | TIMESTAMP | NOT NULL | Expiration time (created_at + 24 hours) |
| accepted_at | TIMESTAMP | NULL | Acceptance time (if accepted) |

**Indexes**:
- PRIMARY KEY on `invitation_id`
- INDEX on `(tenant_id, invited_email, status)` - find pending invites for email
- INDEX on `invited_email` - lookup by email on login
- INDEX on `expires_at` - potential future cleanup queries

**EF Core Entity**:
```csharp
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

public enum InvitationStatus
{
    Pending,
    Accepted,
    Expired,
    Revoked
}
```

---

### 5. CalendarEvent

Represents a calendar event within a tenant's calendar.

**Table**: `calendar_events`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| event_id | UUID | PRIMARY KEY | Unique identifier |
| tenant_id | UUID | FOREIGN KEY (tenants.tenant_id) NOT NULL | Owning tenant |
| creator_id | UUID | FOREIGN KEY (users.user_id) NOT NULL | Event creator |
| assigned_to | UUID | FOREIGN KEY (users.user_id) NULL | Optional assignee |
| title | VARCHAR(255) | NOT NULL | Event title |
| description | TEXT | NULL | Event description |
| start_time | TIMESTAMP | NOT NULL | Event start (UTC) |
| end_time | TIMESTAMP | NOT NULL CHECK (end_time > start_time) | Event end (UTC) |
| created_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Record creation time |
| updated_at | TIMESTAMP | NOT NULL DEFAULT NOW() | Last update time |

**Indexes**:
- PRIMARY KEY on `event_id`
- INDEX on `(tenant_id, start_time)` - optimized date range queries
- INDEX on `creator_id`
- INDEX on `assigned_to`

**EF Core Entity**:
```csharp
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
```

---

## Relationships

### User → Tenant (Owner)
- **Type**: One-to-Many
- **FK**: `Tenant.OwnerId` → `User.UserId`
- **Cascade**: RESTRICT (owner cannot be deleted if tenant exists)

### User ↔ Tenant (Membership)
- **Type**: Many-to-Many through TenantMember
- **FKs**: `TenantMember.UserId` → `User.UserId`, `TenantMember.TenantId` → `Tenant.TenantId`
- **Cascade**: CASCADE (deleting user or tenant removes membership)

### User → Invitation (Inviter)
- **Type**: One-to-Many
- **FK**: `Invitation.InviterId` → `User.UserId`
- **Cascade**: RESTRICT (cannot delete user who sent pending invites)

### Tenant → Invitation
- **Type**: One-to-Many
- **FK**: `Invitation.TenantId` → `Tenant.TenantId`
- **Cascade**: CASCADE (deleting tenant removes invitations)

### User → CalendarEvent (Creator)
- **Type**: One-to-Many
- **FK**: `CalendarEvent.CreatorId` → `User.UserId`
- **Cascade**: SET NULL or RESTRICT (decide: preserve events or block deletion)

### User → CalendarEvent (Assignee)
- **Type**: One-to-Many (optional)
- **FK**: `CalendarEvent.AssignedTo` → `User.UserId`
- **Cascade**: SET NULL (if assignee deleted, event remains unassigned)

### Tenant → CalendarEvent
- **Type**: One-to-Many
- **FK**: `CalendarEvent.TenantId` → `Tenant.TenantId`
- **Cascade**: CASCADE (deleting tenant removes all events)

---

## Multi-Tenant Isolation Strategy

### EF Core Global Query Filters

```csharp
// In ApplicationDbContext.OnModelCreating
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Tenant-scoped entities auto-filter by TenantId
    modelBuilder.Entity<TenantMember>()
        .HasQueryFilter(tm => tm.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

    modelBuilder.Entity<Invitation>()
        .HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

    modelBuilder.Entity<CalendarEvent>()
        .HasQueryFilter(e => e.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());

    // Tenant entity itself filtered by owner or membership
    modelBuilder.Entity<Tenant>()
        .HasQueryFilter(t => t.OwnerId == _tenantProvider.GetUserId() || 
                             _tenantProvider.IsGlobalAdmin() ||
                             t.Members.Any(m => m.UserId == _tenantProvider.GetUserId()));
}
```

### ITenantProvider Service

```csharp
public interface ITenantProvider
{
    Guid GetTenantId();
    Guid GetUserId();
    bool IsGlobalAdmin();
}

public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Guid GetTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id");
        return Guid.Parse(claim?.Value ?? throw new UnauthorizedAccessException("No tenant context"));
    }

    public Guid GetUserId()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim?.Value ?? throw new UnauthorizedAccessException("Not authenticated"));
    }

    public bool IsGlobalAdmin()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst("is_global_admin");
        return bool.TryParse(claim?.Value, out var result) && result;
    }
}
```

---

## Validation Rules

### User
- `GoogleId` must be unique across all users
- `Email` must be valid email format
- `IsGlobalAdmin` defaults to false (only script can set true)

### Tenant
- `Name` required, max 255 characters
- `OwnerId` must reference existing user
- Owner must also exist in TenantMembers with Role='Owner'

### TenantMember
- `(TenantId, UserId)` pair must be unique
- User cannot be assigned to tenant they don't have membership in

### Invitation
- `InvitedEmail` must be valid email format
- Cannot create duplicate pending invitation for same `(TenantId, InvitedEmail)`
- `ExpiresAt` = `CreatedAt` + 24 hours
- Status transitions: Pending → Accepted/Expired/Revoked (no reversals)

### CalendarEvent
- `Title` required, max 255 characters
- `StartTime` must be before `EndTime`
- `AssignedTo` (if provided) must be member of event's tenant
- All timestamps stored in UTC

---

## Migration Strategy

### Initial Migration
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Seed Data
- No seed data for users (admins created via script)
- Consider seed data for demo tenant + sample events (optional)

### Deployment
- Use EF Core migrations in CI/CD pipeline
- Run `dotnet ef database update` on deployment
- Keep migration history in source control

---

## Performance Considerations

### Indexes
- Composite index `(tenant_id, start_time)` on CalendarEvent optimizes date range queries
- Index on `invited_email` speeds up invitation lookups on user login
- Unique constraint on `(tenant_id, user_id)` in TenantMember prevents duplicate memberships

### Query Optimization
- Use `.AsNoTracking()` for read-only queries (event list, invitations)
- Paginate event queries with `.Take(1000)` per spec
- Use `.Include()` judiciously to avoid N+1 queries

### Caching
- Deferred for prototype (measure first per Principle III)
- Candidates: user's tenant memberships, global admin flag

---

## Schema Evolution

### Future Considerations
- Event recurrence patterns (weekly, monthly) → add RecurrenceRule field
- Event location → add Location VARCHAR(500)
- Event reminders → add separate EventReminder table
- Tenant soft delete → add DeletedAt timestamp
- Invitation templates → add InvitationTemplate table

**Decision**: Not implemented for MVP per YAGNI principle (Principle III). Add when validated by usage data.
