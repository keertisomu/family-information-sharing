<!--
Sync Impact Report:
- Version change: 1.1.0 → 2.0.0
- Modified principles: Replaced Fastify with .NET framework (BREAKING CHANGE)
- Added sections: 
  * Core Principles VI (PostgreSQL Data Layer), VII (Authentication & Authorization), VIII (Multi-Tenant Architecture)
  * Data Layer Standards section with PostgreSQL and Entity Framework Core
  * Enhanced API Standards with tenant isolation requirements
- Removed sections: Fastify-specific patterns
- Replaced: Principle II now covers .NET/ASP.NET Core instead of Fastify
- Templates requiring updates:
  ✅ plan-template.md - language agnostic, compatible with .NET
  ✅ spec-template.md - reviewed, compatible with .NET APIs
  ✅ tasks-template.md - reviewed, compatible with .NET project structure
- Follow-up TODOs: None
- Notes: 
  * MAJOR version bump (framework change, backward incompatible)
  * Constitution now mandates .NET/ASP.NET Core as framework
  * PostgreSQL with Entity Framework Core for data access
  * All APIs must implement authentication, authorization, and tenant isolation
  * Multi-tenant architecture is core architectural requirement
-->

# Family Information Sharing API Constitution (.NET)

## Core Principles

### I. API-First Design

Every feature MUST be exposed through a well-defined REST API endpoint. APIs are the primary interface for this service.

**Non-negotiable rules:**
- All endpoints MUST follow RESTful conventions (GET for reads, POST for creates, PUT/PATCH for updates, DELETE for removals)
- Request/response payloads MUST use JSON format
- API contracts MUST be documented before implementation begins
- Each endpoint MUST have clear purpose and responsibility
- Endpoints MUST NOT mix concerns (e.g., a single endpoint should not handle both user creation and authentication)

**Rationale:** As a backend service, the API contract is the foundation. Clear, well-designed endpoints make integration easier and reduce coupling between frontend and backend development.

### II. .NET Framework Standards

All HTTP handling MUST be implemented using ASP.NET Core patterns and best practices.

**Non-negotiable rules:**
- Use ASP.NET Core Minimal APIs or Controllers for endpoint definitions
- Leverage .NET dependency injection for service management
- Use middleware pipeline for cross-cutting concerns (auth, logging, exception handling)
- Follow async/await patterns throughout (Task<T> return types for I/O operations)
- Use built-in model validation attributes and FluentValidation for complex validation
- Utilize route prefixing and API versioning (e.g., `[Route("api/v1/[controller]")]`)
- Use strongly-typed configuration with IOptions<T> pattern

**Rationale:** ASP.NET Core provides robust built-in features for API development including dependency injection, middleware pipeline, and strong typing. Following .NET conventions ensures maintainable, performant, and type-safe code with excellent tooling support.

### III. Rapid Prototyping & Iteration

Speed of iteration takes priority over perfect architecture. Build to learn, refactor when patterns emerge.

**Non-negotiable rules:**
- Start with the simplest solution that could work
- YAGNI principle: You Aren't Gonna Need It - don't build for hypothetical future needs
- Direct implementations preferred over abstraction layers initially
- Premature optimization is forbidden - measure first
- Document assumptions and technical debt for future reference
- Complexity MUST be justified with specific, current needs

**Rationale:** This is a prototype/concept validation project. The goal is to test ideas quickly. Over-engineering slows learning. Refactor when real patterns emerge from usage.

### IV. Data-Driven Validation

All decisions and assumptions MUST be validated through real usage and data, not speculation.

**Non-negotiable rules:**
- Log all meaningful operations (user actions, errors, performance bottlenecks)
- Structure logs for easy parsing and analysis (use structured logging, not string concatenation)
- Track key metrics: response times, error rates, endpoint usage frequency
- Keep error messages informative (include context: what failed, why, what was attempted)
- Return appropriate HTTP status codes (2xx success, 4xx client errors, 5xx server errors)

**Rationale:** In a prototype, understanding what works and what doesn't is critical. Observability enables data-driven iteration and helps identify which features deserve investment.

### V. Simplicity & Clarity

Code MUST be simple, readable, and self-documenting. Favor explicitness over cleverness.

**Non-negotiable rules:**
- Functions/methods should do one thing well
- Variable and function names MUST clearly express intent
- No magic numbers - use named constants
- Avoid deep nesting (max 3 levels) - extract to named functions
- Comments explain WHY, not WHAT (code should be self-explanatory for WHAT)
- Error handling MUST be explicit (no silent failures)

**Rationale:** In rapid prototyping, code changes frequently. Simple code is easier to understand, modify, and debug. Complex code slows iteration and introduces bugs.

### VI. PostgreSQL Data Layer

All persistent data MUST be stored in PostgreSQL. Database interactions MUST follow best practices for reliability and maintainability.

**Non-negotiable rules:**
- Use PostgreSQL as the single source of truth for persistent data
- Use Entity Framework Core as the ORM with code-first migrations
- Leverage PostgreSQL features: JSONB for flexible schemas, indexes for performance, constraints for data integrity
- Use parameterized queries via EF Core (prevents SQL injection)
- Database connections MUST be managed by EF Core's DbContext with connection pooling
- Transactions MUST be used for multi-step operations that need atomicity
- Include TenantId property in all tenant-specific entities (see Principle VIII)
- Use Npgsql as the PostgreSQL provider for .NET

**Rationale:** PostgreSQL provides robust ACID guarantees, rich data types (especially JSONB for flexible schemas in prototypes), and excellent tooling. Entity Framework Core provides type-safe database access with LINQ queries. Migration-based schema management ensures reproducible deployments and clear change history.

## API Standards

### Request/Response Format

- **Content-Type**: `application/json` for all requests and responses
- **Error Format**: `{ "statusCode": 400, "error": "Bad Request", "message": "Detailed error description" }`
- **Success Format**: Return relevant data directly or wrapped in `{ "data": {...} }` for consistency
- **Pagination**: Use query params `?page=1&limit=20` with response metadata `{ "data": [...], "meta": { "page": 1, "limit": 20, "total": 100 } }`

### Authentication & Security

- **Authentication Header**: `Authorization: Bearer <JWT_TOKEN>`
- **JWT Token Contents**: Must include `{ user_id, tenant_id, roles, exp }`
- **Token Expiration**: Reasonable expiry (e.g., 1 hour for access tokens, 7 days for refresh tokens)
- **Login Endpoint**: `POST /api/v1/auth/login` returns `{ access_token, refresh_token, user }`
- **Protected Endpoints**: Extract tenant_id from JWT, use for all database queries
- Use ASP.NET Core Identity for user management and password hashing
- Use Microsoft.AspNetCore.Authentication.JwtBearer for JWT validation
- Sensitive data (passwords, tokens) MUST NOT be logged
- Use HTTPS in production (even for prototypes exposed externally)
- Validate and sanitize all inputs - never trust client data
- Implement rate limiting on authentication endpoints (use AspNetCoreRateLimit)

### Tenant Isolation in APIs

- Every request handler MUST extract tenant_id from authenticated JWT token
- Use EF Core query filters to automatically apply `WHERE TenantId = @tenantId`
- API responses MUST NOT leak tenant_id to clients (internal use only)
- Cross-tenant operations (if needed) require explicit super-admin authorization
- Log all data access with tenant_id for audit trails using ILogger<T>

### VII. Authentication & Authorization

All API endpoints MUST implement authentication and authorization. Access control is non-negotiable.

**Non-negotiable rules:**
- ALL endpoints except health checks MUST require authentication using `[Authorize]` attribute
- Use JWT (JSON Web Tokens) for stateless authentication via Microsoft.AspNetCore.Authentication.JwtBearer
- Tokens MUST include: user_id (sub claim), tenant_id (custom claim), roles (role claim), expiration (exp)
- Implement role-based access control using `[Authorize(Roles = "Admin")]` or policy-based authorization
- Authorization checks MUST happen before any business logic executes (middleware pipeline)
- Failed auth attempts MUST be logged using ILogger (with rate limiting to prevent brute force)
- Use ASP.NET Core Identity for password hashing - NEVER store passwords in plaintext
- Sensitive operations (delete, admin actions) MUST require additional authorization checks via custom policies

**Rationale:** Security cannot be an afterthought in multi-tenant systems. Authentication ensures we know WHO is making the request. Authorization ensures WHAT they can do. ASP.NET Core provides built-in middleware and Identity framework for robust security implementation.

### VIII. Multi-Tenant Architecture

The system MUST support multiple tenants with complete data isolation. Every request and data operation MUST be tenant-aware.

**Non-negotiable rules:**
- Every authenticated request MUST include tenant_id (from JWT token via HttpContext.User claims)
- All database entities storing tenant data MUST have a TenantId property
- Use EF Core global query filters on DbContext to automatically filter by TenantId
- ALL database queries MUST filter by TenantId (prevent cross-tenant data leaks)
- Database constraints SHOULD enforce TenantId presence where applicable
- Tenant isolation MUST be tested - one tenant CANNOT access another's data
- Admin operations that span tenants MUST be explicitly authorized and logged
- Use Row-Level Security (RLS) in PostgreSQL where appropriate for defense-in-depth
- Create a scoped ITenantProvider service to access current tenant context

**Rationale:** Multi-tenancy enables scaling to many customers on shared infrastructure. Data isolation is critical for security, compliance, and customer trust. A single cross-tenant data leak can destroy a business. EF Core query filters provide architecture-level enforcement at the ORM layer.

## Data Layer Standards

### PostgreSQL Best Practices with Entity Framework Core

- **Connection Management**: Use EF Core DbContext with Npgsql provider and connection pooling
- **Query Pattern**: Use LINQ queries via EF Core (compiles to parameterized SQL)
- **Migration Tool**: Use EF Core Migrations (`dotnet ef migrations add`, `dotnet ef database update`)
- **Indexing Strategy**: Use `[Index]` attribute or Fluent API to define indexes on foreign keys, TenantId, and frequently queried columns
- **Schema Patterns**:
  - Include `CreatedAt`, `UpdatedAt` properties (DateTime/DateTimeOffset) on all entities
  - Include `TenantId` property (Guid or int) on all tenant-specific entities
  - Use Guid for primary keys or int with Identity columns
  - Use EF Core's built-in JSON column support for flexible/evolving data structures
  - Implement `IEntity` base interface or abstract class for common properties

### Transaction Management

- Use `await using var transaction = await dbContext.Database.BeginTransactionAsync()` for multi-step operations
- Keep transactions short (acquire late, release early)
- Call `await transaction.CommitAsync()` on success, rollback happens automatically on exception
- Handle transaction rollback on errors explicitly with try-catch
- Log transaction failures with context using ILogger<T>

### Versioning

- API versioning via URL prefix: `/api/v1/resource` using ASP.NET Core API Versioning
- Use `[ApiVersion("1.0")]` attribute on controllers
- MAJOR version bump for breaking changes
- MINOR version bump for backward-compatible additions
- Document breaking changes clearly in changelog

### .NET Specific Standards

- Use nullable reference types (enable `<Nullable>enable</Nullable>` in .csproj)
- Follow async/await best practices: never use `.Result` or `.Wait()`
- Use `IActionResult` or `ActionResult<T>` for controller return types
- Implement health checks using ASP.NET Core Health Checks
- Use structured logging with ILogger and configured sinks (e.g., Serilog)
- Follow Microsoft's REST API Guidelines for .NET

## Governance

### Amendment Process

This constitution can be amended when:
1. Current principles block necessary iteration or learning
2. New patterns emerge that should become standards
3. Technical landscape changes significantly (e.g., framework migration)

Amendments require:
- Documentation of why the change is needed (what problem does it solve?)
- Update to this constitution file with version bump
- Review of affected templates and documentation
- Communication to team members

### Versioning Policy

- **MAJOR**: Backward-incompatible principle changes or principle removal
- **MINOR**: New principles added or existing principles significantly expanded
- **PATCH**: Clarifications, wording improvements, formatting changes

### Compliance

- All feature specifications MUST reference relevant principles
- Implementation plans MUST include a "Constitution Check" section
- Violations of principles MUST be justified in writing (in plan.md complexity tracking)
- This constitution supersedes ad-hoc practices and preferences

**Version**: 2.0.0 | **Ratified**: 2025-11-29 | **Last Amended**: 2025-12-06
