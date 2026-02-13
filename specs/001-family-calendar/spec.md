# Feature Specification: Multi-Tenant Family Calendar System

**Feature Branch**: `001-family-calendar`  
**Created**: 2025-12-06  
**Status**: Draft  
**Input**: User description: "Multi-tenant family calendar with Google OAuth authentication, admin-managed tenants, user invitations, and calendar event management APIs"

## Clarifications

### Session 2025-12-06

- Q: How does the "Admin" role work in relation to tenants? → A: Admin is a separate global admin role that can create and manage tenants
- Q: How are global admin users created and assigned? → A: System admin creation should be a manual process using a script or something that will create the admin in the db, only for tenant owners and users it will be via the endpoint
- Q: What happens after an invitation is created - is there email notification, or purely in-app discovery? → A: An email should be sent to their google email address
- Q: Should calendar events support additional attributes beyond basic fields? → A: Add assigned_to field
- Q: What should happen when an invitation expires? → A: Invitation valid for 1 day, no renewal

### Session 2026-01-25

- Q: When an invitation email fails to send (e.g., SMTP server is down or credentials are invalid), should the system fail the entire invitation creation or create it anyway? → A: Fail the entire invitation creation and return an error to the user
- Q: For the email configuration, should the system use plain text, basic HTML, or external templates? → A: Use basic HTML emails with inline styling (no external templates)
- Q: When a user belongs to multiple tenants, how should the system handle tenant context selection after Google OAuth login? → A: After OAuth, present tenant list; user selects one; JWT includes tenant_id for that session
- Q: For the retry logic when sending emails via SMTP (up to 3 retries with exponential backoff), what should be the timing strategy? → A: Start at 1 second: wait 1s, 4s, 16s (exponential backoff)
- Q: For the unsubscribe link in invitation emails, what should happen when a user clicks unsubscribe? → A: Add user to a "do not email" list; future invitations to their email will not send emails but invitation records are still created

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Creates Tenant for Family (Priority: P1)

A global admin user (with system-level admin privileges) creates a new tenant (family) in the system, establishing an isolated workspace for that family's calendar and users. The admin designates an owner for the tenant.

**Why this priority**: This is the foundation of the multi-tenant system. Without tenants, no other functionality can work. Global admins are the gatekeepers who provision tenant workspaces for families.

**Independent Test**: Can be fully tested by admin authenticating with Google, creating a tenant with a family name, and verifying the tenant exists and is associated with the admin. Delivers a working tenant management system.

**Acceptance Scenarios**:

1. **Given** an authenticated admin user, **When** they create a new tenant with name "Smith Family", **Then** a new tenant is created with a unique tenant_id, the admin is set as owner, and the tenant appears in the admin's tenant list
2. **Given** an authenticated admin user, **When** they attempt to create a tenant with a duplicate name, **Then** the system accepts it (names don't need to be globally unique, only within an admin's scope)
3. **Given** an unauthenticated user, **When** they attempt to create a tenant, **Then** the system returns 401 Unauthorized

---

### User Story 2 - Tenant Owner Invites Family Members (Priority: P2)

A tenant owner invites family members to join their family calendar by sending invitations to their Google email addresses. The system sends an email notification to the invitee with instructions to accept the invitation.

**Why this priority**: After tenant creation, the owner needs to add family members. Email notifications ensure invitees are aware of the invitation without requiring them to proactively check the app.

**Independent Test**: Can be tested by a tenant owner sending invitations to email addresses and verifying invitation records are created. Delivers a working invitation system without requiring full event management.

**Acceptance Scenarios**:

1. **Given** a tenant owner is authenticated, **When** they invite "john@gmail.com" to their tenant, **Then** an invitation record is created with status "pending" and an email notification is successfully delivered to john@gmail.com
2. **Given** a tenant owner is authenticated, **When** they invite a user but the email service is unavailable, **Then** the invitation creation fails and returns an error message indicating the email could not be sent
3. **Given** a tenant member (non-owner) is authenticated, **When** they attempt to invite a user, **Then** the system returns 403 Forbidden
4. **Given** an invited user with "john@gmail.com", **When** they authenticate with Google using that email, **Then** they can accept the invitation and become a member of the tenant
5. **Given** an invitation to "john@gmail.com", **When** a user authenticates with "different@gmail.com", **Then** they cannot access or accept that invitation

---

### User Story 6 - Family Members Manage Calendar Events (Priority: P3)

Authenticated family members can create, view, edit, and delete calendar events within their tenant's calendar.

**Why this priority**: This is the core functionality of the calendar system. However, it requires tenants and members to exist first, making it P3.

**Independent Test**: Can be tested by tenant members performing CRUD operations on events and verifying changes persist and are visible to all tenant members. Delivers the core calendar functionality.

**Acceptance Scenarios**:

1. **Given** an authenticated tenant member, **When** they create an event with title "Family Dinner" on 2025-12-15 at 18:00, **Then** the event is saved with tenant_id, creator_id, and appears in the calendar (assigned_to is optional)
2. **Given** an authenticated tenant member, **When** they view the calendar, **Then** they see all events for their tenant only (no cross-tenant data leaks)
3. **Given** an authenticated tenant member, **When** they edit an event's title from "Meeting" to "Important Meeting", **Then** the event is updated and the change is visible to all tenant members
4. **Given** an authenticated tenant member, **When** they delete an event, **Then** the event is removed and no longer visible to any tenant member
5. **Given** a user who is not a member of a tenant, **When** they attempt to view that tenant's events, **Then** the system returns 403 Forbidden

---

### User Story 4 - User Selects Tenant Context After Login (Priority: P2)

When a user who belongs to multiple tenants authenticates via Google OAuth, they must select which tenant context to work in before accessing tenant-scoped resources. The system provides a list of available tenants and generates a tenant-scoped JWT token upon selection.

**Why this priority**: Users may be part of multiple family calendars (e.g., immediate family and extended family). Clear tenant selection prevents confusion and accidental data access across tenants. This is essential before any tenant-scoped operations (events, invitations) can work correctly.

**Independent Test**: Can be tested by creating a user in multiple tenants, authenticating, listing tenants, selecting one, and verifying the JWT token contains the correct tenant_id. Delivers tenant context management.

**Acceptance Scenarios**:

1. **Given** a user who is a member of multiple tenants authenticates with Google, **When** they request their tenant list, **Then** all tenants they belong to are returned with tenant names and their role in each
2. **Given** a user has received their tenant list, **When** they select a specific tenant, **Then** a JWT token is generated containing that tenant_id and their role in that tenant
3. **Given** a user with a tenant-scoped JWT token, **When** they access tenant-scoped endpoints (events, invitations), **Then** all data is automatically filtered to their selected tenant without requiring additional parameters
4. **Given** a user is currently working in one tenant, **When** they want to switch to another tenant, **Then** they can select the new tenant and receive a new JWT token scoped to that tenant
5. **Given** a user who belongs to only one tenant authenticates with Google, **When** they complete OAuth, **Then** they still must go through tenant selection (list will show one tenant)

---

### User Story 5 - Retrieve and Filter Calendar Events (Priority: P4)

Tenant members can retrieve calendar events for up to 4 weeks/month and filter events by searching names or descriptions.

**Why this priority**: Viewing and filtering are essential for usability, but the basic CRUD operations (P3) must work first. This enhances the calendar's usefulness.

**Independent Test**: Can be tested by creating events across multiple weeks, querying with date ranges and search filters, and verifying correct results are returned. Delivers enhanced calendar querying capabilities.

**Acceptance Scenarios**:

1. **Given** an authenticated tenant member, **When** they request events for the next 4 weeks, **Then** only events within that date range for their tenant are returned
2. **Given** multiple events exist ("Doctor Appointment", "Dentist Visit", "Birthday Party"), **When** a member searches for "doctor", **Then** only "Doctor Appointment" is returned (case-insensitive match on name or description)
3. **Given** events exist with descriptions containing "holiday", **When** a member searches for "holiday", **Then** all matching events are returned regardless of the search term appearing in name or description
4. **Given** an authenticated tenant member, **When** they request events from 2025-12-01 to 2025-12-31, **Then** only events within December 2025 for their tenant are returned, even if there are events in November or January

---

### Edge Cases

- What happens when a user is invited to multiple tenants? (After OAuth login, system displays list of all tenants user belongs to; user must select one to receive tenant-scoped JWT; user can switch tenants anytime via tenant selection endpoint)
- How does the system handle an invitation to an email address that is already a member? (Should return a clear error message)
- What happens if an invitation expires before acceptance? (Status changes to expired after 24 hours, user cannot accept; owner must create new invitation)
- What happens when an event spans multiple days? (Store start and end timestamps, query should include events that overlap the date range)
- How does the system handle timezone differences for events? (Store all timestamps in UTC, return with timezone info, client handles display)
- What happens when a tenant owner leaves? (Owner cannot be removed; must transfer ownership first or delete tenant)
- How does the system handle revoked Google OAuth tokens? (Return 401, require re-authentication)
- What happens when searching with special characters? (Sanitize input, perform safe text search, return empty array if no matches)
- What happens when requesting events beyond 4 weeks? (Return error or limit to 4 weeks maximum, document in API)
- What happens when SMTP server is down or credentials are invalid? (Invitation creation fails with appropriate error message; user must retry later when service is restored; no partial invitation record created)
- What happens when invited email address is invalid or doesn't exist? (SMTP server may accept initially but bounce later; for immediate validation errors, invitation creation fails; for delayed bounces, invitation exists but invitee won't receive it)
- How does the system handle SMTP rate limits? (Some SMTP providers have rate limits; implement exponential backoff already handles this; for high-volume usage, consider dedicated email service provider)
- What happens when SMTP configuration is missing or invalid? (System logs error at startup; invitation creation will fail when attempting to send emails; admin must fix config)
- What happens when switching SMTP servers? (Update configuration and restart application; existing invitations maintain their delivery status)
- What happens when a user unsubscribes from invitation emails? (User's email_opt_out flag is set to true; future invitations are created but no email is sent; user can still accept invitations in-app and can re-enable emails later)
- What happens when inviting a user who has opted out of emails? (Invitation record is created successfully without attempting to send email; invitation remains valid for acceptance)

## Requirements *(mandatory)*

### Functional Requirements

**Authentication & Authorization:**

- **FR-001**: System MUST authenticate all users via Google OAuth 2.0
- **FR-002**: System MUST extract user's email, name, and Google user ID from OAuth token and create user record automatically (unless already exists)
- **FR-003**: System MUST generate JWT tokens containing user_id, tenant_id, and roles after user selects a tenant (not immediately after OAuth)
- **FR-003a**: System MUST provide endpoint to list all tenants a user belongs to after successful OAuth authentication
- **FR-003b**: System MUST provide endpoint for user to select a tenant, which generates a tenant-scoped JWT token
- **FR-003c**: System MUST include tenant_id in JWT payload to automatically scope all subsequent API requests to that tenant
- **FR-003d**: System MUST allow users to switch tenants by selecting a different tenant and receiving a new JWT token
- **FR-004**: System MUST protect all API endpoints (except OAuth callback, tenant selection, and health check) with authentication
- **FR-005**: System MUST enforce role-based access control with three distinct roles: Global Admin (system-level, can create/manage any tenant), Owner (tenant-level, manages their specific tenant), Member (tenant-level, participates in tenant)

**Tenant Management:**

- **FR-006**: System MUST allow global admin users to create new tenants with a name, description, and designated owner
- **FR-007**: System MUST assign the owner designated by the global admin during tenant creation (admin does not become the owner)
- **FR-008**: System MUST ensure complete data isolation between tenants
- **FR-009**: System MUST allow tenant owners to view and manage their tenant's settings
- **FR-010**: System MUST allow tenant owners to delete their tenants (with confirmation)
- **FR-010a**: System MUST allow global admins to view, manage, and delete any tenant
- **FR-010b**: System MUST restrict tenant creation to users with global admin role only
- **FR-010c**: System MUST support manual admin creation via database script or command-line tool (not via API endpoint)

**User Invitation:**

- **FR-011**: System MUST allow tenant owners to invite users via Google email address and send email notification to the invitee
- **FR-011a**: System MUST send email notification containing tenant name, inviter name, and instructions to accept invitation (unless invitee has opted out of emails)
- **FR-011b**: System MUST use SMTP-based email service for sending invitation emails
- **FR-011c**: System MUST allow configuration of SMTP settings via environment variables or configuration file
- **FR-011d**: System MUST validate email provider configuration on application startup and log warnings if misconfigured
- **FR-012**: System MUST create invitation records with status (pending, accepted, expired)
- **FR-012a**: System MUST automatically expire invitations after 24 hours from creation time
- **FR-012b**: System MUST prevent acceptance of expired invitations
- **FR-013**: System MUST validate that invited email is a valid email format
- **FR-014**: System MUST allow invited users to accept invitations upon authentication
- **FR-015**: System MUST prevent duplicate invitations to the same email for the same tenant
- **FR-016**: System MUST allow tenant owners to revoke pending invitations (marks as revoked, no renewal mechanism)
- **FR-017**: System MUST allow tenant owners to remove members from the tenant

**Calendar Event Management:**

- **FR-018**: System MUST allow authenticated tenant members to create calendar events
- **FR-019**: System MUST store event with: title, description, start time, end time, assigned_to (FK to User, nullable), creator_id, tenant_id
- **FR-020**: System MUST allow authenticated tenant members to view all events in their tenant
- **FR-021**: System MUST allow authenticated tenant members to edit events
- **FR-022**: System MUST allow authenticated tenant members to delete events
- **FR-023**: System MUST prevent cross-tenant access to events (strict tenant isolation)
- **FR-024**: System MUST track event creation and modification timestamps

**Email Notification Management:**

- **FR-024a**: System MUST use SMTP protocol for email delivery via MailKit library
- **FR-024b**: System MUST require the following SMTP configuration:
  - SMTP host and port
  - Username and password (optional for anonymous SMTP servers)
  - SSL/TLS setting (StartTls, SslOnConnect, or None)
  - From email address and name
- **FR-024c**: System MUST validate SMTP configuration on startup and fail gracefully if required settings are missing
- **FR-024d**: System MUST log all email sending attempts with status (success, failure) and SMTP server used
- **FR-024e**: System MUST fail invitation creation and return an error to the user when email sending fails after retries
- **FR-024f**: System MUST retry failed email sends up to 3 times with exponential backoff (wait 1 second, then 4 seconds, then 16 seconds) before failing the invitation creation
- **FR-024g**: System MUST sanitize email content to prevent injection attacks
- **FR-024h**: System MUST generate HTML emails with inline CSS styling (no external stylesheets or templates)
- **FR-024i**: System MUST provide plain text fallback for HTML emails for email clients that don't support HTML
- **FR-024j**: System MUST include unsubscribe link in footer of all invitation emails that allows recipients to opt out of future email notifications
- **FR-024k**: System MUST provide unsubscribe endpoint that sets user's email_opt_out flag to true when accessed
- **FR-024l**: System MUST skip email sending for users with email_opt_out=true while still creating invitation records
- **FR-024m**: System MUST allow users to re-enable email notifications by updating their email_opt_out flag to false

**Event Retrieval & Filtering:**

- **FR-025**: System MUST provide API endpoint to retrieve events within a date range (up to 4 weeks/1 month)
- **FR-026**: System MUST support filtering events by text search on title and description
- **FR-027**: System MUST perform case-insensitive text search
- **FR-028**: System MUST return events sorted by start time (ascending)
- **FR-029**: System MUST return empty array when no events match the criteria
- **FR-030**: System MUST enforce maximum date range of 4 weeks for event retrieval

**Data Integrity:**

- **FR-031**: System MUST use UTC for all timestamp storage
- **FR-032**: System MUST validate event start time is before end time
- **FR-032a**: System MUST validate that assigned_to user (if provided) is a member of the event's tenant
- **FR-033**: System MUST ensure tenant_id is set on all tenant-scoped data
- **FR-034**: System MUST log all authentication attempts and authorization failures

### Key Entities

- **User**: Represents a person authenticated via Google OAuth. Attributes: user_id (GUID), google_id (string), email (string), name (string), is_global_admin (boolean), email_opt_out (boolean, default false), created_at, updated_at. A user can be a member of multiple tenants with different roles (Owner/Member), and separately may have global admin privileges. Users who opt out of emails will not receive invitation email notifications but can still be invited.

- **Tenant**: Represents a family or group calendar workspace. Attributes: tenant_id (GUID), name (string), description (string), owner_id (FK to User), created_at, updated_at. Provides complete data isolation boundary.

- **TenantMember**: Represents the relationship between a user and a tenant. Attributes: tenant_member_id (GUID), tenant_id (FK), user_id (FK), role (enum: Owner, Member), joined_at. Defines who can access which tenant and their permissions.

- **Invitation**: Represents a pending or completed invitation to join a tenant. Attributes: invitation_id (GUID), tenant_id (FK), invited_email (string), inviter_id (FK to User), status (enum: Pending, Accepted, Expired, Revoked), email_sent_at (timestamp, nullable - null if invitee opted out), created_at, expires_at (created_at + 24 hours), accepted_at. Tracks invitation lifecycle. Email is sent unless the invitee has email_opt_out enabled. Invitations expire after 24 hours and cannot be renewed.

- **CalendarEvent**: Represents a calendar event within a tenant. Attributes: event_id (GUID), tenant_id (FK), creator_id (FK to User), assigned_to (FK to User, nullable), title (string), description (text), start_time (timestamp), end_time (timestamp), created_at, updated_at. Events can optionally be assigned to a specific tenant member. All events belong to exactly one tenant.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin users can create a new tenant and become owner in under 30 seconds after authentication
- **SC-002**: Tenant owners can invite a family member and have the invitation record created in under 10 seconds
- **SC-003**: Invited users can authenticate with Google and accept invitation in under 1 minute
- **SC-004**: Tenant members can create a calendar event and see it appear in the calendar view in under 5 seconds
- **SC-005**: Tenant members can retrieve 4 weeks of events with search filter in under 1 second (for up to 1000 events)
- **SC-006**: System maintains 100% tenant isolation (zero cross-tenant data leaks in security testing)
- **SC-007**: Calendar event CRUD operations complete successfully for 95% of requests
- **SC-008**: Google OAuth authentication succeeds for 99% of valid Google accounts
- **SC-009**: Event search returns accurate results matching title or description for 100% of queries
- **SC-010**: System supports at least 10 concurrent tenants with 5 members each without performance degradation
- **SC-011**: Invitation emails are delivered successfully within 30 seconds when invitation creation succeeds (includes up to 21 seconds for retry attempts: 1s + 4s + 16s)
- **SC-012**: Email sending failures are logged with sufficient detail for troubleshooting (provider, error code, timestamp) and clear error messages returned to users
- **SC-013**: System returns appropriate error messages when email provider is unavailable, allowing users to retry invitation creation later

### Assumptions

- Global admin users are provisioned manually by system administrators before system use (via script/tool)
- JWT tokens are tenant-scoped; users must select a tenant after OAuth to receive a token; switching tenants requires obtaining a new token
- Invitation expiry is enforced at check time (no background job required initially; expires_at comparison on acceptance)
- Users have valid Google accounts and consent to OAuth permissions
- Users access the system via standard web browsers or API clients
- Calendar events are primarily used for family coordination (not enterprise-scale scheduling)
- Date range queries limited to 4 weeks is acceptable for prototype phase
- SMTP email service is configured before system deployment:
  - SMTP server host and port are available
  - SMTP credentials (username/password) are configured if required
  - Sender email address is configured
  - SSL/TLS settings match SMTP server requirements
- SMTP configuration is managed via environment variables or secure configuration files (not hardcoded)
- Email delivery failures block invitation creation (synchronous delivery with retries); users must retry when service is restored
- Initial implementation uses single email provider per environment (no dynamic switching or failover)
- Tenant deletion is permanent (no soft delete or recovery mechanism needed initially)
- Event editing permissions are uniform (all tenant members can edit all events)
- Timezone handling is delegated to client applications (API works in UTC)
- Invitation creation may take up to 21 seconds due to email retry logic (1s + 4s + 16s exponential backoff) before failing
- Email HTML content is generated programmatically with inline styles (no external template files)
