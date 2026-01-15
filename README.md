# Family Calendar API

Multi-tenant REST API for family calendar management with Google OAuth authentication, invitation system, and event management.

## Features

- **Multi-Tenant Architecture**: Complete data isolation between family workspaces
- **Google OAuth 2.0**: Secure authentication using Google accounts
- **User Invitations**: Email-based invitation system with 24-hour expiry
- **Calendar Events**: Full CRUD operations with filtering and text search
- **Role-Based Access**: Global Admin, Tenant Owner, and Member roles

## Technology Stack

- **.NET 8.0** (LTS)
- **ASP.NET Core 8.0** - Web API
- **Entity Framework Core 8.0** - ORM
- **PostgreSQL 15+** - Database
- **Google.Apis.Auth** - OAuth authentication
- **MailKit** - Email notifications
- **JWT** - Token-based authentication

## Quick Start

### Prerequisites

- .NET 8.0 SDK (or Docker for containerized setup)
- PostgreSQL 15+ (or use Docker Compose)
- Google OAuth credentials
- SMTP server access (Gmail or Mailtrap)

### Option 1: Docker Setup (Recommended)

The easiest way to get started:

```bash
# Clone repository
git clone https://github.com/your-org/family-calendar-api.git
cd family-calendar-api

# Update appsettings.Development.json with your credentials
cp src/FamilyCalendar.Api/appsettings.json src/FamilyCalendar.Api/appsettings.Development.json
# Edit appsettings.Development.json with actual Google OAuth and SMTP credentials

# Start PostgreSQL and API with Docker Compose
docker-compose up -d

# Wait for services to be healthy (about 10 seconds)
docker-compose ps

# Run database migrations
docker-compose exec api dotnet ef database update

# Create global admin user (update with your Google ID)
docker-compose exec postgres psql -U postgres -d family_calendar -c \
  "INSERT INTO users (user_id, google_id, email, name, is_global_admin, created_at, updated_at) 
   VALUES (gen_random_uuid(), 'YOUR_GOOGLE_USER_ID', 'admin@yourdomain.com', 'Admin User', true, NOW(), NOW());"

# View logs
docker-compose logs -f api
```

API will be available at `http://localhost:5000`

**Useful Docker Commands:**

```bash
# Stop services
docker-compose down

# Restart services
docker-compose restart

# View PostgreSQL logs
docker-compose logs postgres

# Access PostgreSQL shell
docker-compose exec postgres psql -U postgres -d family_calendar

# Rebuild API image after code changes
docker-compose build api
docker-compose up -d api
```

### Option 2: Local Installation

If you prefer running services locally:

```bash
# Clone repository
git clone https://github.com/your-org/family-calendar-api.git
cd family-calendar-api

# Restore dependencies
dotnet restore

# Update appsettings.Development.json with your credentials
cp src/FamilyCalendar.Api/appsettings.json src/FamilyCalendar.Api/appsettings.Development.json
# Edit appsettings.Development.json with actual credentials

# Create database
createdb family_calendar

# Run migrations
dotnet ef database update --project src/FamilyCalendar.Api

# Create global admin user (update with your Google ID)
./scripts/create-admin.sh --google-id "YOUR_GOOGLE_USER_ID" --email "admin@yourdomain.com" --name "Admin User"

# Run the API
dotnet run --project src/FamilyCalendar.Api
```

API will be available at `http://localhost:5000`

### Configuration

Update `appsettings.Development.json` with your settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=family_calendar;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "Jwt": {
      "SecretKey": "YOUR_SECURE_SECRET_KEY_32_CHARS_MIN",
      "Issuer": "FamilyCalendar",
      "Audience": "FamilyCalendar"
    }
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseTls": true,
    "Username": "YOUR_EMAIL",
    "Password": "YOUR_APP_PASSWORD"
  }
}
```

## API Endpoints

### Authentication
- `GET /api/v1/auth/google/login` - Initiate Google OAuth
- `GET /api/v1/auth/google/callback` - OAuth callback
- `POST /api/v1/auth/refresh` - Refresh access token
- `POST /api/v1/auth/select-tenant` - Switch tenant context

### Tenants
- `POST /api/v1/tenants` - Create tenant (admin only)
- `GET /api/v1/tenants` - List tenants
- `GET /api/v1/tenants/{id}` - Get tenant details
- `PATCH /api/v1/tenants/{id}` - Update tenant
- `DELETE /api/v1/tenants/{id}` - Delete tenant

### Invitations
- `POST /api/v1/tenants/{id}/invitations` - Create invitation
- `GET /api/v1/invitations` - List user's invitations
- `POST /api/v1/invitations/{id}/accept` - Accept invitation
- `DELETE /api/v1/invitations/{id}` - Revoke invitation

### Events
- `POST /api/v1/events` - Create event
- `GET /api/v1/events` - List events (with filters)
- `GET /api/v1/events/{id}` - Get event details
- `PUT /api/v1/events/{id}` - Update event
- `DELETE /api/v1/events/{id}` - Delete event

## Development

### Project Structure

```
family-information-sharing/
├── src/
│   └── FamilyCalendar.Api/
│       ├── Controllers/         # API endpoints
│       ├── Models/              # Entity models
│       ├── Services/            # Business logic
│       ├── Data/                # EF Core DbContext
│       └── Middleware/          # Custom middleware
├── scripts/
│   └── create-admin.sh          # Admin provisioning
├── specs/
│   └── 001-family-calendar/     # Feature specifications
└── FamilyCalendar.sln           # Solution file
```

### Running Tests

```bash
# No tests included (prototype scope per constitution)
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add MigrationName --project src/FamilyCalendar.Api

# Apply migrations
dotnet ef database update --project src/FamilyCalendar.Api

# Rollback migration
dotnet ef database update PreviousMigrationName --project src/FamilyCalendar.Api
```

## Architecture

### Multi-Tenant Isolation

- Every entity includes `TenantId` for tenant-scoped data
- EF Core global query filters automatically enforce isolation
- JWT tokens contain `tenant_id` claim
- ITenantProvider service extracts tenant context from HTTP request

### Authentication Flow

1. User clicks "Login with Google"
2. Redirects to Google OAuth
3. Google validates and returns authorization code
4. Backend exchanges code for user profile
5. Backend creates/updates User record
6. Backend generates JWT tokens (access + refresh)
7. All subsequent API calls use JWT Bearer token

## Documentation

- **Quickstart**: `specs/001-family-calendar/quickstart.md`
- **Feature Spec**: `specs/001-family-calendar/spec.md`
- **Implementation Plan**: `specs/001-family-calendar/plan.md`
- **Data Model**: `specs/001-family-calendar/data-model.md`
- **API Contracts**: `specs/001-family-calendar/contracts/`
- **Tasks**: `specs/001-family-calendar/tasks.md`

## Contributing

This is a prototype project following rapid iteration principles. See `.specify/memory/constitution.md` for architectural guidelines.

## License

[Your License Here]

## Support

For issues and questions, please refer to the documentation in `specs/001-family-calendar/` or create an issue in the repository.
