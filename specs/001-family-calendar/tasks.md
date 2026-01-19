# Tasks: Multi-Tenant Family Calendar System

**Input**: Design documents from `/specs/001-family-calendar/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are OPTIONAL per constitution (prototype scope) - not included in this task list

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- **API Project**: `src/FamilyCalendar.Api/`
- **Controllers**: `src/FamilyCalendar.Api/Controllers/`
- **Models**: `src/FamilyCalendar.Api/Models/`
- **Services**: `src/FamilyCalendar.Api/Services/`
- **Data**: `src/FamilyCalendar.Api/Data/`
- **Scripts**: `scripts/`

---

## Phase 1: Setup (Shared Infrastructure) ✅ COMPLETE

**Purpose**: Project initialization and basic structure

- [x] T001 Create .NET solution and API project structure in src/FamilyCalendar.Api/
- [x] T002 Install NuGet packages (ASP.NET Core 8.0, EF Core 8.0, Npgsql, Google.Apis.Auth, MailKit)
- [x] T003 [P] Create appsettings.json with database connection strings, Google OAuth config, JWT config, SMTP config
- [x] T004 [P] Configure .gitignore for appsettings.Development.json and sensitive files
- [x] T005 [P] Create README.md with quickstart instructions from specs/001-family-calendar/quickstart.md

---

## Phase 1.5: Docker Setup (Infrastructure) ✅ COMPLETE

**Purpose**: Containerize services for consistent development environment

- [x] T005a [P] Create docker-compose.yml for PostgreSQL 15 with environment variables and volume persistence
- [x] T005b [P] Create Dockerfile for FamilyCalendar.Api with multi-stage build (build + runtime)
- [x] T005c [P] Update README.md with Docker commands (docker-compose up, database initialization)

---

## Phase 2: Foundational (Blocking Prerequisites) ✅ COMPLETE

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**✅ COMPLETE**: Foundation is ready - user story implementation can now proceed

### Database & ORM Foundation

- [x] T006 Create ApplicationDbContext in src/FamilyCalendar.Api/Data/ApplicationDbContext.cs with DbSet properties
- [x] T007 [P] Create User entity in src/FamilyCalendar.Api/Models/User.cs (UserId, GoogleId, Email, Name, IsGlobalAdmin)
- [x] T008 [P] Create Tenant entity in src/FamilyCalendar.Api/Models/Tenant.cs (TenantId, Name, Description, OwnerId)
- [x] T009 [P] Create TenantMember entity in src/FamilyCalendar.Api/Models/TenantMember.cs (TenantMemberId, TenantId, UserId, Role)
- [x] T010 [P] Create Invitation entity in src/FamilyCalendar.Api/Models/Invitation.cs (InvitationId, TenantId, InvitedEmail, InviterId, Status, ExpiresAt)
- [x] T011 [P] Create CalendarEvent entity in src/FamilyCalendar.Api/Models/CalendarEvent.cs (EventId, TenantId, CreatorId, AssignedTo, Title, Description, StartTime, EndTime)
- [x] T012 Configure entity relationships and indexes in ApplicationDbContext.OnModelCreating()
- [x] T013 Create initial EF Core migration (dotnet ef migrations add InitialCreate)
- [x] T014 Create ITenantProvider interface in src/FamilyCalendar.Api/Services/ITenantProvider.cs (GetTenantId, GetUserId, IsGlobalAdmin)
- [x] T015 Implement TenantProvider in src/FamilyCalendar.Api/Services/TenantProvider.cs extracting tenant_id from HttpContext.User claims
- [x] T016 Configure EF Core global query filters in ApplicationDbContext for multi-tenant isolation (TenantMember, Invitation, CalendarEvent)

### Authentication & Authorization Foundation

- [x] T017 Configure JWT authentication in Program.cs with Microsoft.AspNetCore.Authentication.JwtBearer
- [x] T018 Configure Google OAuth in Program.cs with AddGoogle() extension
- [x] T019 [P] Create JwtTokenService in src/FamilyCalendar.Api/Services/JwtTokenService.cs (GenerateAccessToken, GenerateRefreshToken)
- [x] T020 [P] Create IEmailService interface in src/FamilyCalendar.Api/Services/IEmailService.cs
- [x] T021 [P] Implement SmtpEmailService with MailKit in src/FamilyCalendar.Api/Services/SmtpEmailService.cs

### Middleware & Infrastructure

- [x] T022 [P] Create global exception handling middleware in src/FamilyCalendar.Api/Middleware/ExceptionMiddleware.cs
- [x] T023 [P] Configure structured logging with ILogger<T> in Program.cs
- [x] T024 [P] Configure CORS policies in Program.cs
- [x] T025 Configure dependency injection for all services (ITenantProvider, JwtTokenService, IEmailService) in Program.cs
- [x] T026 Create health check endpoint in Program.cs (GET /health)

**Checkpoint**: ✅ Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Admin Creates Tenant for Family (Priority: P1) ✅ COMPLETE

**Goal**: Global admin can create tenants and designate owners, establishing isolated workspaces

**Independent Test**: Admin authenticates with Google, creates tenant with family name, verifies tenant exists and owner assigned

### Implementation for User Story 1

- [x] T027 [P] [US1] Create AuthController in src/FamilyCalendar.Api/Controllers/AuthController.cs (skeleton)
- [x] T028 [P] [US1] Create TenantsController in src/FamilyCalendar.Api/Controllers/TenantsController.cs (skeleton with [Authorize] attribute)
- [x] T029 [US1] Implement GET /api/v1/auth/google/login in AuthController (redirects to Google OAuth)
- [x] T030 [US1] Implement GET /api/v1/auth/google/callback in AuthController (validates code, creates/updates User, generates JWT tokens)
- [x] T031 [US1] Implement POST /api/v1/auth/refresh in AuthController (validates refresh token, generates new access token)
- [x] T032 [US1] Implement POST /api/v1/auth/select-tenant in AuthController (validates tenant membership, generates new JWT with updated tenant_id)
- [x] T033 [US1] Implement POST /api/v1/tenants in TenantsController (global admin only, creates tenant with designated owner, validates owner email exists)
- [x] T034 [US1] Implement GET /api/v1/tenants in TenantsController (global admin sees all tenants)
- [x] T035 [US1] Implement GET /api/v1/tenants/{tenantId} in TenantsController (admin/owner/member can view their tenant)
- [x] T036 [US1] Implement PATCH /api/v1/tenants/{tenantId} in TenantsController (admin/owner only, updates name/description)
- [x] T037 [US1] Implement DELETE /api/v1/tenants/{tenantId} in TenantsController (admin/owner only, deletes tenant and cascades)
- [x] T038 [US1] Implement GET /api/v1/tenants/{tenantId}/members in TenantsController (returns tenant member list)
- [x] T039 [US1] Add validation and error handling for all tenant endpoints (400 for invalid data, 403 for authorization failures, 404 for not found)
- [x] T040 [US1] Add structured logging for tenant operations (create, update, delete with tenant_id and user_id)
- [x] T041 [US1] Create bash script scripts/create-admin.sh for manual global admin provisioning (PostgreSQL INSERT)

**Checkpoint**: At this point, User Story 1 should be fully functional - admin can authenticate and manage tenants

---

## Phase 4: User Story 2 - Tenant Owner Invites Family Members (Priority: P2) ✅ COMPLETE

**Goal**: Tenant owners can invite members via email with automatic email notifications

**Independent Test**: Tenant owner sends invitation to email address, verifies invitation record created and email sent, invited user authenticates and accepts invitation

### Implementation for User Story 2

- [x] T042 [P] [US2] Create InvitationsController in src/FamilyCalendar.Api/Controllers/InvitationsController.cs (skeleton with [Authorize])
- [x] T043 [US2] Implement POST /api/v1/tenants/{tenantId}/invitations in InvitationsController (owner only, creates invitation, calculates expires_at as created_at + 24h)
- [x] T044 [US2] Integrate IEmailService in POST /api/v1/tenants/{tenantId}/invitations to send email notification (tenant name, inviter name, accept URL)
- [x] T045 [US2] Implement GET /api/v1/tenants/{tenantId}/invitations in InvitationsController (owner retrieves all tenant invitations with optional status filter)
- [x] T046 [US2] Implement GET /api/v1/invitations in InvitationsController (user retrieves their pending invitations by email, filters status=Pending and not expired)
- [x] T047 [US2] Implement POST /api/v1/invitations/{invitationId}/accept in InvitationsController (validates email match, not expired, creates TenantMember with role=Member)
- [x] T048 [US2] Implement DELETE /api/v1/invitations/{invitationId} in InvitationsController (owner revokes pending invitation, sets status=Revoked)
- [x] T049 [US2] Implement DELETE /api/v1/tenants/{tenantId}/members in TenantsController (owner removes member, validates not removing self)
- [x] T050 [US2] Add validation for invitations (email format, duplicate check, expiry check on acceptance)
- [x] T051 [US2] Add error handling for invitation endpoints (400 for expired/duplicate, 403 for non-owner, 404 for not found)
- [x] T052 [US2] Add structured logging for invitation operations (create, accept, revoke with tenant_id, inviter_id, invited_email)

**Checkpoint**: ✅ At this point, User Stories 1 AND 2 should both work independently - admin creates tenants, owners invite members

---

## Phase 5: User Story 3 - Family Members Manage Calendar Events (Priority: P3)

**Goal**: Tenant members can create, view, edit, and delete calendar events with tenant isolation

**Independent Test**: Tenant member performs CRUD operations on events and verifies changes persist and are visible to all tenant members, cross-tenant access blocked

### Implementation for User Story 3

- [x] T053 [P] [US3] Create EventsController in src/FamilyCalendar.Api/Controllers/EventsController.cs (skeleton with [Authorize])
- [x] T054 [US3] Implement POST /api/v1/events in EventsController (creates event with tenant_id from JWT, validates start_time < end_time)
- [x] T055 [US3] Add validation for assigned_to field in POST /api/v1/events (if provided, must be tenant member)
- [x] T056 [US3] Implement GET /api/v1/events/{eventId} in EventsController (tenant-scoped, returns single event)
- [x] T057 [US3] Implement PUT /api/v1/events/{eventId} in EventsController (updates event, validates start_time < end_time and assigned_to membership)
- [x] T058 [US3] Implement DELETE /api/v1/events/{eventId} in EventsController (hard delete event)
- [x] T059 [US3] Add error handling for event endpoints (400 for invalid times/assignment, 403 for cross-tenant access, 404 for not found)
- [x] T060 [US3] Add structured logging for event operations (create, update, delete with event_id, tenant_id, user_id)

**Checkpoint**: ✅ At this point, User Stories 1, 2, AND 3 should all work independently - full event CRUD within tenant boundaries

---

## Phase 6: User Story 4 - Retrieve and Filter Calendar Events (Priority: P4)

**Goal**: Tenant members can retrieve events with date range filtering (max 4 weeks) and text search

**Independent Test**: Create events across multiple weeks, query with date ranges and search filters, verify correct results returned

### Implementation for User Story 4

- [ ] T061 [US4] Implement GET /api/v1/events in EventsController with query parameters (startDate required, endDate required, search optional, assignedTo optional, page, limit)
- [ ] T062 [US4] Add date range validation in GET /api/v1/events (max 4 weeks span between startDate and endDate)
- [ ] T063 [US4] Implement case-insensitive text search on title and description using ILIKE in GET /api/v1/events query
- [ ] T064 [US4] Implement filtering by assignedTo user in GET /api/v1/events query
- [ ] T065 [US4] Implement pagination in GET /api/v1/events (page and limit parameters, return meta with total count)
- [ ] T066 [US4] Add sorting by start_time ascending in GET /api/v1/events query
- [ ] T067 [US4] Optimize query with composite index on (tenant_id, start_time) per data-model.md
- [ ] T068 [US4] Add error handling for invalid date ranges (400 for exceeding 4 weeks)
- [ ] T069 [US4] Add structured logging for event queries (search text, date range, tenant_id)

**Checkpoint**: All user stories should now be independently functional - complete calendar system with filtering

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T070 [P] Add API versioning middleware (api/v1 prefix already in routes)
- [ ] T071 [P] Configure Swagger/OpenAPI documentation in Program.cs
- [ ] T072 [P] Add rate limiting on authentication endpoints using AspNetCoreRateLimit
- [ ] T073 [P] Add database connection pooling configuration in appsettings.json (Npgsql defaults to 100 max connections)
- [ ] T074 Code cleanup and refactoring (extract common validation logic, consolidate error responses)
- [ ] T075 Security review (ensure no passwords in logs, validate HTTPS in production, check CORS policies)
- [ ] T076 [P] Create Docker Compose file for PostgreSQL + API deployment
- [ ] T077 Run quickstart.md validation (verify setup steps work end-to-end)
- [ ] T078 [P] Update README.md with API documentation links and architecture diagram

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately ✅ Complete
- **Docker Setup (Phase 1.5)**: Depends on Setup completion - Provides containerized PostgreSQL for development
- **Foundational (Phase 2)**: Depends on Docker Setup completion (optional) or local PostgreSQL - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational completion - No dependencies on other stories
- **User Story 2 (Phase 4)**: Depends on Foundational completion - Integrates with US1 (requires tenants to exist) but independently testable
- **User Story 3 (Phase 5)**: Depends on Foundational completion - Integrates with US1/US2 (requires tenants and members) but independently testable
- **User Story 4 (Phase 6)**: Depends on US3 completion (requires events to exist for filtering)
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - Creates foundation for all other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Requires US1 tenants but independently testable (create tenant, then test invitations)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Requires US1 tenants and US2 members but independently testable (create tenant, add member, then test events)
- **User Story 4 (P4)**: Must complete after US3 - Extends event querying, requires events to exist

### Within Each User Story

- Controllers (skeleton) before endpoint implementations
- Core CRUD endpoints before validation/error handling
- Error handling before logging
- Story complete before moving to next priority

### Parallel Opportunities

**Setup Phase (Phase 1)** ✅ Complete:
- T003 (appsettings), T004 (.gitignore), T005 (README) can all run in parallel

**Docker Setup Phase (Phase 1.5)**:
- T005a (docker-compose), T005b (Dockerfile), T005c (README update) can all run in parallel

**Foundational Phase (Phase 2)**:
- T007-T011 (all entity models) can run in parallel
- T019 (JwtTokenService), T020-T021 (IEmailService), T022 (exception middleware), T023 (logging), T024 (CORS) can run in parallel after entity models

**User Story 1 (Phase 3)**:
- T027 (AuthController skeleton), T028 (TenantsController skeleton) can run in parallel
- After T028 complete: T033-T038 (all tenant CRUD endpoints) can be implemented in parallel by different developers

**User Story 2 (Phase 4)**:
- After T042 (InvitationsController skeleton): T043-T048 (all invitation endpoints) can be implemented in parallel

**User Story 3 (Phase 5)**:
- After T053 (EventsController skeleton): T054-T058 (all event CRUD endpoints) can be implemented in parallel

**User Story 4 (Phase 6)**:
- T061-T066 are sequential (same GET /api/v1/events endpoint implementation)

**Polish Phase (Phase 7)**:
- T070 (versioning), T071 (Swagger), T072 (rate limiting), T073 (pooling), T076 (Docker), T078 (README) can all run in parallel

---

## Parallel Example: User Story 1

```bash
# After Foundational phase completes, launch all tenant endpoints in parallel:
Task: "Implement POST /api/v1/tenants in TenantsController"          # Developer A
Task: "Implement GET /api/v1/tenants in TenantsController"           # Developer B
Task: "Implement GET /api/v1/tenants/{tenantId} in TenantsController" # Developer C
Task: "Implement PATCH /api/v1/tenants/{tenantId} in TenantsController" # Developer D
Task: "Implement DELETE /api/v1/tenants/{tenantId} in TenantsController" # Developer E
Task: "Implement GET /api/v1/tenants/{tenantId}/members in TenantsController" # Developer F
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) - Recommended Start

1. Complete Phase 1: Setup ✅ (~2 hours)
2. Complete Phase 1.5: Docker Setup (~30 minutes)
3. Complete Phase 2: Foundational (CRITICAL - blocks all stories) (~1 day)
4. Complete Phase 3: User Story 1 (~1 day)
5. **STOP and VALIDATE**: Test authentication and tenant management independently
6. Deploy/demo if ready - you now have a working multi-tenant admin system

**MVP Deliverable**: Global admins can authenticate with Google, create tenants, designate owners, manage tenant settings

### Incremental Delivery

1. Complete Setup + Docker + Foundational → Foundation ready (~1.5-2 days)
2. Add User Story 1 → Test independently → Deploy/Demo (MVP! ~1 day)
3. Add User Story 2 → Test independently → Deploy/Demo (~1 day)
4. Add User Story 3 → Test independently → Deploy/Demo (~1 day)
5. Add User Story 4 → Test independently → Deploy/Demo (~0.5 days)
6. Polish Phase → Production-ready (~0.5 days)

**Total Estimated Time**: 5.5-6.5 days for full implementation

### Parallel Team Strategy

With multiple developers:

1. **Day 1**: Team completes Setup + Docker together (quick setup)
2. **Day 1-2**: Team completes Foundational together (pair programming recommended for foundation)
3. **Day 2+**: Once Foundational is done:
   - **Developer A**: User Story 1 (Authentication + Tenants)
   - **Developer B**: User Story 2 (Invitations) - can start after US1 completes
   - **Developer C**: User Story 3 (Events) - can start after US1/US2 complete
   - **Developer D**: User Story 4 (Filtering) - can start after US3 completes
3. Stories complete and integrate independently, merge to main as each completes

**Parallel Benefits**: With 2-3 developers, can complete in 3-4 days instead of 5-6 days

---

## Task Counts & Statistics

**Total Tasks**: 81

**By Phase**:
- Phase 1 (Setup): 5 tasks ✅ Complete
- Phase 1.5 (Docker Setup): 3 tasks
- Phase 2 (Foundational): 21 tasks (CRITICAL PATH)
- Phase 3 (US1 - Admin Creates Tenants): 15 tasks
- Phase 4 (US2 - Invite Members): 11 tasks
- Phase 5 (US3 - Event CRUD): 8 tasks
- Phase 6 (US4 - Event Filtering): 9 tasks
- Phase 7 (Polish): 9 tasks

**Parallelizable Tasks**: 28 tasks marked with [P] (~35% of total)

**Independent Test Criteria per Story**:
- **US1**: Admin authenticates, creates tenant "Test Family", verifies tenant in GET /api/v1/tenants, updates name, deletes tenant
- **US2**: Owner invites "test@example.com", checks invitation in DB and email inbox, invited user authenticates and accepts, verifies now in tenant members
- **US3**: Member creates event "Family Dinner" on 2025-12-15, retrieves via GET /api/v1/events/{eventId}, updates title, deletes event
- **US4**: Create 10 events across January 2026, query GET /api/v1/events?startDate=2026-01-01&endDate=2026-01-28&search=dinner, verify only matching events returned with pagination

**Suggested MVP Scope**: Phase 1 + Phase 1.5 + Phase 2 + Phase 3 (User Story 1 only) = 44 tasks for working multi-tenant admin system

---

## Notes

- [P] tasks = different files, no dependencies, can run in parallel
- [Story] label maps task to specific user story for traceability (US1, US2, US3, US4)
- Each user story should be independently completable and testable
- Tests are OPTIONAL per constitution (prototype scope) - not included
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- API paths use `/api/v1/` prefix per OpenAPI contracts
- All timestamps stored in UTC per data-model.md
- Multi-tenant isolation enforced via EF Core query filters (set up in Foundational phase)
- Global admin users created manually via scripts/create-admin.sh (Task T041)
