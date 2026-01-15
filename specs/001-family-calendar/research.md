# Research: Multi-Tenant Family Calendar System

**Feature**: [spec.md](./spec.md)  
**Phase**: 0 - Outline & Research  
**Date**: 2025-12-06

## Purpose

Resolve all NEEDS CLARIFICATION items from Technical Context and research best practices for key technology choices to inform Phase 1 design decisions.

## Research Areas

### 1. Google OAuth 2.0 Integration with ASP.NET Core

**Question**: How to implement Google OAuth authentication and extract user profile data?

**Decision**: Use `Google.Apis.Auth` NuGet package for token validation + ASP.NET Core external authentication

**Rationale**:
- `Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync()` validates Google ID tokens
- ASP.NET Core's `AddGoogle()` extension provides OAuth flow out-of-the-box
- Can extract email, name, google_id from validated token claims
- Integrates with ASP.NET Core Identity if needed (optional for prototype)

**Implementation Approach**:
```csharp
// In Program.cs
services.AddAuthentication()
    .AddGoogle(options => {
        options.ClientId = configuration["Google:ClientId"];
        options.ClientSecret = configuration["Google:ClientSecret"];
    })
    .AddJwtBearer(options => { /* JWT validation */ });
```

**Alternatives Considered**:
- Manual OAuth flow implementation → Rejected (reinventing wheel, error-prone)
- Third-party auth providers (Auth0, Okta) → Rejected (adds external dependency, overkill for prototype)

---

### 2. JWT Token Generation and Multi-Tenant Claims

**Question**: How to structure JWT tokens to support multi-tenant context switching?

**Decision**: Include `tenant_id` as custom claim, support multiple tenants per user via claim arrays or context selection

**Rationale**:
- User can be member of multiple tenants (spec allows this)
- JWT should include currently selected `tenant_id` for API requests
- Alternative: Omit `tenant_id` from JWT, require `X-Tenant-ID` header (more flexible but less conventional)

**Implementation Approach**:
```csharp
var claims = new List<Claim> {
    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
    new Claim(ClaimTypes.Email, user.Email),
    new Claim("tenant_id", selectedTenantId.ToString()),
    new Claim("is_global_admin", user.IsGlobalAdmin.ToString()),
    new Claim(ClaimTypes.Role, role) // Owner, Member, or GlobalAdmin
};
```

**Best Practice**: Use `X-Tenant-ID` header for tenant selection, validate against user's memberships in middleware

**Alternatives Considered**:
- Separate JWT per tenant → Rejected (token proliferation, complex management)
- No `tenant_id` in JWT, always query from DB → Rejected (performance hit, defeats stateless auth purpose)

---

### 3. Entity Framework Core Multi-Tenant Query Filters

**Question**: How to automatically enforce tenant isolation at the ORM level?

**Decision**: Use EF Core Global Query Filters on DbContext with scoped `ITenantProvider` service

**Rationale**:
- `modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => e.TenantId == tenantProvider.GetTenantId())` auto-applies filter
- Scoped `ITenantProvider` extracts `tenant_id` from HttpContext claims once per request
- Prevents accidental cross-tenant queries (defense-in-depth per Principle VIII)
- Can be disabled for global admin queries via `.IgnoreQueryFilters()`

**Implementation Approach**:
```csharp
public interface ITenantProvider {
    Guid GetTenantId();
    bool IsGlobalAdmin();
}

// In DbContext.OnModelCreating
modelBuilder.Entity<CalendarEvent>()
    .HasQueryFilter(e => e.TenantId == _tenantProvider.GetTenantId() || _tenantProvider.IsGlobalAdmin());
```

**Alternatives Considered**:
- Manual `WHERE TenantId = ` in every LINQ query → Rejected (error-prone, easy to forget)
- Row-Level Security in PostgreSQL → Considered as secondary defense layer (can add later)

---

### 4. Email Service Integration for Invitation Notifications

**Question**: Which email service to use for sending invitation emails?

**Decision**: Use MailKit (SMTP) for prototype, design abstraction for future SendGrid/SES migration

**Rationale**:
- MailKit is free, works with any SMTP provider (Gmail, Outlook, local dev)
- No API key management or external service costs for prototype
- `IEmailService` abstraction allows swapping to SendGrid/SES later without changing business logic

**Implementation Approach**:
```csharp
public interface IEmailService {
    Task SendInvitationEmailAsync(string toEmail, string tenantName, string inviterName, string acceptUrl);
}

// MailKit implementation for prototype
public class SmtpEmailService : IEmailService {
    // Use MailKit's SmtpClient
}
```

**Configuration**:
```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "noreply@familycalendar.com",
    "Password": "[app-specific-password]",
    "From": "FamilyCalendar <noreply@familycalendar.com>"
  }
}
```

**Alternatives Considered**:
- SendGrid API → Deferred (requires API key, billing setup; overkill for prototype)
- AWS SES → Deferred (same reasons as SendGrid)
- No abstraction, hardcode MailKit → Rejected (violates Principle V: harder to test and migrate)

---

### 5. Admin Provisioning Script Strategy

**Question**: How to implement manual admin creation script per clarification?

**Decision**: Create bash script that connects to PostgreSQL and inserts admin user record

**Rationale**:
- Spec clarifies "manual process using a script" for admin creation
- Script can accept Google user ID, email, name as parameters
- Idempotent (check if admin already exists before inserting)
- No API exposure reduces attack surface

**Implementation Approach**:
```bash
#!/bin/bash
# scripts/create-admin.sh
GOOGLE_ID=$1
EMAIL=$2
NAME=$3

psql $DATABASE_URL -c "
INSERT INTO users (user_id, google_id, email, name, is_global_admin, created_at, updated_at)
VALUES (gen_random_uuid(), '$GOOGLE_ID', '$EMAIL', '$NAME', true, NOW(), NOW())
ON CONFLICT (google_id) DO UPDATE SET is_global_admin = true;
"
```

**Alternatives Considered**:
- Seed data in EF migrations → Rejected (not truly "manual", admins hardcoded in code)
- Admin API endpoint with secret key → Rejected (spec says manual, not API)
- Direct database access only → Accepted (most secure, aligns with spec)

---

### 6. Invitation Expiry Enforcement Strategy

**Question**: How to enforce 24-hour invitation expiry without background jobs?

**Decision**: Check `expires_at` timestamp at acceptance time, no background job needed for prototype

**Rationale**:
- Spec clarifies "expiry enforced at check time" in assumptions
- Simple: `WHERE status = 'Pending' AND expires_at > NOW()` in acceptance query
- No scheduler/worker needed (reduces complexity per Principle III)
- Expired invitations remain in DB for audit (no cleanup job)

**Implementation Approach**:
```csharp
// In InvitationsController.AcceptInvitation
var invitation = await db.Invitations
    .Where(i => i.InvitationId == id && 
                i.Status == InvitationStatus.Pending && 
                i.ExpiresAt > DateTime.UtcNow)
    .FirstOrDefaultAsync();

if (invitation == null) {
    return BadRequest("Invitation not found or expired");
}
```

**Alternatives Considered**:
- Background job to mark expired invitations → Deferred (adds Hangfire/Quartz dependency, premature optimization)
- Database trigger to auto-update status → Rejected (complicates debugging, less explicit)

---

### 7. PostgreSQL Connection Pooling Best Practices

**Question**: How to configure EF Core connection pooling for optimal performance?

**Decision**: Use Npgsql's built-in connection pooling with default settings, monitor and tune if needed

**Rationale**:
- Npgsql enables pooling by default (no config required)
- Default pool size (100 connections) sufficient for prototype scale (10 tenants * 5 users = 50 concurrent)
- Can tune via connection string: `Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50`
- EF Core DbContext as scoped service ensures proper connection lifecycle

**Implementation Approach**:
```csharp
// In Program.cs
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

**Connection String**:
```
Host=localhost;Database=familycalendar;Username=postgres;Password=***;Pooling=true;
```

**Alternatives Considered**:
- Manual connection management → Rejected (EF Core handles this well)
- Singleton DbContext → Rejected (not thread-safe, violates EF Core patterns)

---

### 8. Event Date Range Query Optimization

**Question**: How to efficiently query events within 4-week range with optional text search?

**Decision**: Create composite index on `(tenant_id, start_time)` and use PostgreSQL full-text search for description

**Rationale**:
- Most queries filter by tenant (via query filter) and date range
- Composite index `(tenant_id, start_time)` enables efficient range scans
- For text search: Use `ILIKE` for simple prototype, upgrade to full-text search (GIN index) if slow
- Limit result set to 1000 events per spec success criteria

**Implementation Approach**:
```csharp
// In DbContext.OnModelCreating
modelBuilder.Entity<CalendarEvent>()
    .HasIndex(e => new { e.TenantId, e.StartTime });

// Query implementation
var events = await db.CalendarEvents
    .Where(e => e.StartTime >= startDate && e.EndTime <= endDate)
    .Where(e => string.IsNullOrEmpty(searchText) || 
                e.Title.Contains(searchText) || 
                e.Description.Contains(searchText))
    .OrderBy(e => e.StartTime)
    .Take(1000)
    .ToListAsync();
```

**Alternatives Considered**:
- Full-text search from day 1 → Deferred (premature optimization, ILIKE sufficient for prototype)
- No index → Rejected (violates Principle IV: measure first, but indexing is standard practice)

---

## Resolved Unknowns Summary

| Unknown | Resolution |
|---------|------------|
| Google OAuth integration | `Google.Apis.Auth` package + ASP.NET Core `AddGoogle()` |
| JWT multi-tenant structure | `tenant_id` as custom claim + `X-Tenant-ID` header pattern |
| EF Core tenant isolation | Global query filters with scoped `ITenantProvider` |
| Email service | MailKit (SMTP) with `IEmailService` abstraction |
| Admin provisioning | Bash script with direct PostgreSQL INSERT |
| Invitation expiry | Check `expires_at` at acceptance, no background job |
| Connection pooling | Npgsql defaults (built-in, 100 max connections) |
| Event query optimization | Composite index `(tenant_id, start_time)` + ILIKE search |

## Next Phase Dependencies

**Phase 1 - Design** can now proceed with:
- Complete data model with all entity properties and relationships
- API contract definitions for all 4 controller areas (Auth, Tenants, Invitations, Events)
- Quickstart guide with Google OAuth setup, PostgreSQL config, and admin script usage

**No remaining NEEDS CLARIFICATION markers** - all technical decisions documented and justified.
