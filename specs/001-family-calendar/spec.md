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

1. **Given** a tenant owner is authenticated, **When** they invite "john@gmail.com" to their tenant, **Then** an invitation record is created with status "pending" and the invitee's email is stored
2. **Given** a tenant member (non-owner) is authenticated, **When** they attempt to invite a user, **Then** the system returns 403 Forbidden
3. **Given** an invited user with "john@gmail.com", **When** they authenticate with Google using that email, **Then** they can accept the invitation and become a member of the tenant
4. **Given** an invitation to "john@gmail.com", **When** a user authenticates with "different@gmail.com", **Then** they cannot access or accept that invitation

---

### User Story 3 - Family Members Manage Calendar Events (Priority: P3)

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

### User Story 4 - Retrieve and Filter Calendar Events (Priority: P4)

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

- What happens when a user is invited to multiple tenants? (User should be able to switch context between tenants)
- How does the system handle an invitation to an email address that is already a member? (Should return a clear error message)
- What happens if an invitation expires before acceptance? (Status changes to expired after 24 hours, user cannot accept; owner must create new invitation)
- What happens when an event spans multiple days? (Store start and end timestamps, query should include events that overlap the date range)
- How does the system handle timezone differences for events? (Store all timestamps in UTC, return with timezone info, client handles display)
- What happens when a tenant owner leaves? (Owner cannot be removed; must transfer ownership first or delete tenant)
- How does the system handle revoked Google OAuth tokens? (Return 401, require re-authentication)
- What happens when searching with special characters? (Sanitize input, perform safe text search, return empty array if no matches)
- What happens when requesting events beyond 4 weeks? (Return error or limit to 4 weeks maximum, document in API)

## Requirements *(mandatory)*

### Functional Requirements

**Authentication & Authorization:**

- **FR-001**: System MUST authenticate all users via Google OAuth 2.0
- **FR-002**: System MUST extract user's email, name, and Google user ID from OAuth token and create user record automatically (unless already exists)
- **FR-003**: System MUST generate JWT tokens containing user_id, tenant_id, and roles after successful authentication
- **FR-004**: System MUST protect all API endpoints (except OAuth callback and health check) with authentication
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
- **FR-011a**: System MUST send email notification containing tenant name, inviter name, and instructions to accept invitation
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

- **User**: Represents a person authenticated via Google OAuth. Attributes: user_id (GUID), google_id (string), email (string), name (string), is_global_admin (boolean), created_at, updated_at. A user can be a member of multiple tenants with different roles (Owner/Member), and separately may have global admin privileges.

- **Tenant**: Represents a family or group calendar workspace. Attributes: tenant_id (GUID), name (string), description (string), owner_id (FK to User), created_at, updated_at. Provides complete data isolation boundary.

- **TenantMember**: Represents the relationship between a user and a tenant. Attributes: tenant_member_id (GUID), tenant_id (FK), user_id (FK), role (enum: Owner, Member), joined_at. Defines who can access which tenant and their permissions.

- **Invitation**: Represents a pending or completed invitation to join a tenant. Attributes: invitation_id (GUID), tenant_id (FK), invited_email (string), inviter_id (FK to User), status (enum: Pending, Accepted, Expired, Revoked), created_at, expires_at (created_at + 24 hours), accepted_at. Tracks invitation lifecycle. Invitations expire after 24 hours and cannot be renewed.

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

### Assumptions

- Global admin users are provisioned manually by system administrators before system use (via script/tool)
- Invitation expiry is enforced at check time (no background job required initially; expires_at comparison on acceptance)
- Users have valid Google accounts and consent to OAuth permissions
- Users access the system via standard web browsers or API clients
- Calendar events are primarily used for family coordination (not enterprise-scale scheduling)
- Date range queries limited to 4 weeks is acceptable for prototype phase
- Email service integration (SMTP/SendGrid/SES) is available for sending invitation notifications
- Tenant deletion is permanent (no soft delete or recovery mechanism needed initially)
- Event editing permissions are uniform (all tenant members can edit all events)
- Timezone handling is delegated to client applications (API works in UTC)
