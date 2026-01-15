# Implementation Plan: Multi-Tenant Family Calendar System

**Branch**: `001-family-calendar` | **Date**: 2025-12-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-family-calendar/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a multi-tenant REST API for family calendar management with Google OAuth authentication, global admin tenant provisioning, email-based user invitations, and comprehensive calendar event CRUD operations with tenant isolation. Technical approach uses ASP.NET Core Web API with Entity Framework Core, PostgreSQL database, JWT authentication, and email service integration.

## Technical Context

**Language/Version**: C# / .NET 8.0 (LTS)  
**Primary Dependencies**: ASP.NET Core 8.0, Entity Framework Core 8.0, Npgsql (PostgreSQL provider), Google.Apis.Auth (OAuth), MailKit or SendGrid (email)  
**Storage**: PostgreSQL 15+ with Npgsql EF Core provider  
**Testing**: Prototype scope - testing optional per constitution  
**Target Platform**: Linux/Windows server (Docker deployment)  
**Project Type**: Single API project (backend only)  
**Performance Goals**: <1s event queries (1000 events), <5s event CRUD, <30s tenant creation  
**Constraints**: <200ms p95 for API responses, 24-hour invitation expiry, 4-week max date range for queries  
**Scale/Scope**: 10 concurrent tenants, 5 members each, ~500 events per tenant for prototype validation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: API-First Design ✅ PASS
- **Requirement**: All features via REST endpoints with RESTful conventions
- **Compliance**: Spec defines clear API endpoints for tenants, invitations, events, auth
- **Status**: No violations

### Principle II: .NET Framework Standards ✅ PASS
- **Requirement**: ASP.NET Core, dependency injection, middleware, async/await
- **Compliance**: Plan uses ASP.NET Core Web API, EF Core, standard .NET patterns
- **Status**: No violations

### Principle III: Rapid Prototyping & Iteration ✅ PASS
- **Requirement**: Simplest solution, YAGNI, no premature optimization
- **Compliance**: Direct implementations (no repository pattern initially), 24h invitation expiry (no renewal complexity), optional assigned_to field only
- **Status**: No violations

### Principle IV: Data-Driven Validation ✅ PASS
- **Requirement**: Structured logging, metrics, appropriate status codes
- **Compliance**: ILogger<T> for all operations, HTTP status codes per spec, auth/authz logging
- **Status**: No violations

### Principle V: Simplicity & Clarity ✅ PASS
- **Requirement**: Simple, readable code; explicit error handling
- **Compliance**: Clear entity model, straightforward CRUD operations, explicit validation
- **Status**: No violations

### Principle VI: PostgreSQL Data Layer ✅ PASS
- **Requirement**: PostgreSQL with EF Core, migrations, parameterized queries, TenantId on entities
- **Compliance**: All 5 entities (User, Tenant, TenantMember, Invitation, CalendarEvent) have TenantId where appropriate, EF Core migrations planned
- **Status**: No violations

### Principle VII: Authentication & Authorization ✅ PASS
- **Requirement**: JWT auth, role-based access control, ASP.NET Core Identity
- **Compliance**: Google OAuth → JWT flow, three-tier roles (GlobalAdmin/Owner/Member), [Authorize] attributes
- **Status**: No violations

### Principle VIII: Multi-Tenant Architecture ✅ PASS
- **Requirement**: TenantId on all tenant data, EF Core query filters, complete isolation
- **Compliance**: TenantId on TenantMember, Invitation, CalendarEvent entities; global query filters planned; FR-008, FR-023 mandate isolation
- **Status**: No violations

**Overall Result**: ✅ **ALL GATES PASS** - Proceed to Phase 0 research

---

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 design (data model, API contracts, quickstart) completion.*

### Review Date: 2025-12-06

All 8 constitution principles remain compliant after detailed design:

1. **API-First Design** ✅ - 4 OpenAPI specifications created (auth, tenants, invitations, events) with complete schemas
2. **.NET Framework Standards** ✅ - ASP.NET Core controllers, EF Core entities, dependency injection patterns throughout
3. **Rapid Prototyping** ✅ - No over-engineering; direct implementations; deferred features (event recurrence, full-text search, background jobs)
4. **Data-Driven Validation** ✅ - Structured logging via ILogger<T>; appropriate HTTP status codes in all contracts
5. **Simplicity & Clarity** ✅ - Clear entity relationships; straightforward CRUD operations; explicit validation rules
6. **PostgreSQL Data Layer** ✅ - Complete data model with 5 entities, indexes, constraints, and EF Core migration strategy
7. **Authentication & Authorization** ✅ - Google OAuth flow documented; JWT structure defined; role-based access control in all contracts
8. **Multi-Tenant Architecture** ✅ - TenantId on all tenant entities; ITenantProvider interface designed; EF Core query filters specified

**Complexity Tracking**: No principle violations. All complexity justified by requirements:
- Multi-tenant isolation (required by spec)
- Google OAuth integration (required by spec)
- Email notifications (required by spec)
- Three-tier role model (required by clarifications)

**Final Status**: ✅ **ALL GATES PASS POST-DESIGN**

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── FamilyCalendar.Api/              # ASP.NET Core Web API project
│   ├── Controllers/                 # API controllers
│   │   ├── AuthController.cs       # Google OAuth & JWT
│   │   ├── TenantsController.cs    # Tenant CRUD (admin only)
│   │   ├── InvitationsController.cs # Invite & accept flows
│   │   └── EventsController.cs      # Calendar event CRUD & queries
│   ├── Models/                      # EF Core entities
│   │   ├── User.cs
│   │   ├── Tenant.cs
│   │   ├── TenantMember.cs
│   │   ├── Invitation.cs
│   │   └── CalendarEvent.cs
│   ├── Data/                        # EF Core DbContext & migrations
│   │   ├── ApplicationDbContext.cs
│   │   └── Migrations/
│   ├── Services/                    # Business logic
│   │   ├── IGoogleAuthService.cs
│   │   ├── IEmailService.cs
│   │   ├── ITenantService.cs
│   │   └── IEventService.cs
│   ├── Middleware/                  # Custom middleware
│   │   └── TenantContextMiddleware.cs
│   ├── Extensions/                  # Service registration extensions
│   │   └── ServiceCollectionExtensions.cs
│   ├── DTOs/                        # Request/response models
│   │   ├── Auth/
│   │   ├── Tenants/
│   │   ├── Invitations/
│   │   └── Events/
│   ├── Program.cs                   # Application entry point
│   ├── appsettings.json            # Configuration
│   └── FamilyCalendar.Api.csproj   # Project file
├── scripts/                         # Admin provisioning scripts
│   └── create-admin.sh             # Manual admin creation
└── docker-compose.yml              # PostgreSQL + API containers

.gitignore
README.md
```

**Structure Decision**: Single ASP.NET Core Web API project (Option 1 pattern). This is a backend-only REST API service with no frontend. The structure follows standard .NET conventions:
- Controllers for HTTP endpoints (Principle I: API-First)
- Models for EF Core entities (Principle VI: PostgreSQL Data Layer)
- Services for business logic (Principle II: Dependency Injection)
- Data/Migrations for EF Core schema management
- DTOs for request/response contracts
- Middleware for cross-cutting concerns (auth, logging, tenant context)

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
