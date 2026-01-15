# Quickstart Guide: Family Calendar API

## Overview

This guide walks you through setting up the Family Calendar API on your local machine for development and testing.

**Technology Stack:**
- .NET 8.0 SDK (LTS)
- PostgreSQL 15+
- Google OAuth 2.0 (authentication)
- MailKit (SMTP email notifications)

**Estimated setup time:** 15-20 minutes

---

## Prerequisites

### 1. Install .NET 8.0 SDK

**Windows:**
```bash
winget install Microsoft.DotNet.SDK.8
```

**macOS:**
```bash
brew install dotnet-sdk
```

**Linux (Ubuntu):**
```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

Verify installation:
```bash
dotnet --version  # Should show 8.0.x
```

### 2. Install PostgreSQL 15+

**Windows:**
- Download from https://www.postgresql.org/download/windows/
- Use default port 5432
- Remember the `postgres` user password

**macOS:**
```bash
brew install postgresql@15
brew services start postgresql@15
```

**Linux (Ubuntu):**
```bash
sudo apt update
sudo apt install postgresql-15
sudo systemctl start postgresql
```

Verify installation:
```bash
psql --version  # Should show 15.x or higher
```

### 3. Configure Google OAuth 2.0

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or select existing)
3. Enable **Google+ API**
4. Go to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure consent screen:
   - User Type: External
   - Scopes: `email`, `profile`, `openid`
6. Create OAuth Client ID:
   - Application type: Web application
   - Authorized redirect URIs: `http://localhost:5000/api/v1/auth/google/callback`
7. **Save the Client ID and Client Secret** (needed below)

### 4. Configure SMTP for Email Notifications

For development, use a test SMTP service:

**Option A: Gmail (for testing only)**
- Email: your-gmail@gmail.com
- Password: [Create App Password](https://myaccount.google.com/apppasswords)
- SMTP Server: smtp.gmail.com
- Port: 587
- Enable TLS: true

**Option B: Mailtrap (recommended for dev)**
- Sign up at https://mailtrap.io
- Get SMTP credentials from your inbox settings

---

## Setup Instructions

### 1. Clone Repository

```bash
git clone https://github.com/your-org/family-calendar-api.git
cd family-calendar-api
```

### 2. Configure Application Settings

Create `appsettings.Development.json` in the API project root:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=family_calendar_dev;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "Jwt": {
      "SecretKey": "dev-secret-key-min-32-chars-12345",
      "Issuer": "http://localhost:5000",
      "Audience": "http://localhost:5000",
      "AccessTokenExpirationMinutes": 60,
      "RefreshTokenExpirationDays": 7
    }
  },
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseTls": true,
    "FromEmail": "noreply@familycalendar.dev",
    "FromName": "Family Calendar",
    "Username": "YOUR_EMAIL_USERNAME",
    "Password": "YOUR_EMAIL_APP_PASSWORD"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

**Security Note:** Never commit `appsettings.Development.json` to version control (already in `.gitignore`).

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Create Database

```bash
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE family_calendar_dev;

# Exit psql
\q
```

### 5. Run EF Core Migrations

```bash
# Install EF Core tools (if not already installed)
dotnet tool install --global dotnet-ef

# Apply migrations to create database schema
dotnet ef database update --project src/FamilyCalendar.Api
```

**Expected output:**
```
Build started...
Build succeeded.
Applying migration '20251206_InitialCreate'...
Done.
```

### 6. Create Global Admin User

The system requires a global admin to create tenants. Use the provisioning script:

```bash
# Navigate to scripts directory
cd scripts

# Make script executable (Linux/macOS)
chmod +x create-admin.sh

# Run script with your Google account details
./create-admin.sh \
  --google-id "YOUR_GOOGLE_USER_ID" \
  --email "admin@yourdomain.com" \
  --name "Admin User"
```

**Finding your Google User ID:**
1. Go to https://www.google.com/settings/account
2. Your User ID is in the URL: `https://myaccount.google.com/u/0/?pageId=YOUR_USER_ID`

**Windows users:** Use Git Bash or WSL to run the script, or manually insert into PostgreSQL:

```sql
psql -U postgres -d family_calendar_dev
INSERT INTO users (user_id, google_id, email, name, is_global_admin, created_at, updated_at)
VALUES (
  gen_random_uuid(),
  'YOUR_GOOGLE_USER_ID',
  'admin@yourdomain.com',
  'Admin User',
  true,
  NOW(),
  NOW()
);
```

### 7. Run the API

```bash
# From project root
dotnet run --project src/FamilyCalendar.Api
```

**Expected output:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

---

## Verification

### 1. Health Check

```bash
curl http://localhost:5000/health
```

**Expected response:**
```json
{
  "status": "Healthy",
  "database": "Connected",
  "timestamp": "2025-12-06T10:00:00Z"
}
```

### 2. Authentication Flow

**Step 1: Initiate Google OAuth**

Open in browser:
```
http://localhost:5000/api/v1/auth/google/login
```

- Redirects to Google login
- Select your admin account
- Redirects back with JWT tokens

**Step 2: Test Authenticated Endpoint**

```bash
# Replace YOUR_JWT_TOKEN with the accessToken from login
curl -X GET http://localhost:5000/api/v1/tenants \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Expected response (empty list initially):**
```json
{
  "data": [],
  "meta": {
    "page": 1,
    "limit": 20,
    "total": 0
  }
}
```

### 3. Create Test Tenant

```bash
curl -X POST http://localhost:5000/api/v1/tenants \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Family",
    "description": "Test tenant for verification",
    "ownerEmail": "admin@yourdomain.com"
  }'
```

**Expected response:**
```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Test Family",
  "description": "Test tenant for verification",
  "ownerId": "123e4567-e89b-12d3-a456-426614174000",
  "ownerName": "Admin User",
  "ownerEmail": "admin@yourdomain.com",
  "createdAt": "2025-12-06T10:00:00Z",
  "updatedAt": "2025-12-06T10:00:00Z"
}
```

### 4. Verify Email Notifications

Create an invitation to trigger email:

```bash
curl -X POST http://localhost:5000/api/v1/tenants/YOUR_TENANT_ID/invitations \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "invitedEmail": "test@example.com"
  }'
```

Check your SMTP service (Gmail sent folder or Mailtrap inbox) for the invitation email.

---

## API Documentation

Once the API is running, access interactive API docs:

**Swagger UI:**
```
http://localhost:5000/swagger
```

Browse all endpoints, schemas, and try requests directly from the browser.

**OpenAPI Specification:**
- Authentication: `specs/001-family-calendar/contracts/auth-api.yaml`
- Tenants: `specs/001-family-calendar/contracts/tenants-api.yaml`
- Invitations: `specs/001-family-calendar/contracts/invitations-api.yaml`
- Events: `specs/001-family-calendar/contracts/events-api.yaml`

---

## Common Issues

### Issue: "Database connection failed"

**Solution:**
1. Verify PostgreSQL is running: `pg_isready`
2. Check connection string in `appsettings.Development.json`
3. Ensure database exists: `psql -U postgres -l | grep family_calendar_dev`

### Issue: "Google OAuth redirect_uri_mismatch"

**Solution:**
1. Verify redirect URI in Google Cloud Console matches exactly: `http://localhost:5000/api/v1/auth/google/callback`
2. No trailing slashes
3. Must use `http` for localhost (not `https`)

### Issue: "Email sending failed"

**Solution:**
1. For Gmail: Ensure App Password is used (not account password)
2. For Gmail: Enable "Less secure app access" if using older accounts
3. Check SMTP credentials in `appsettings.Development.json`
4. Review logs: `dotnet run --project src/FamilyCalendar.Api | grep Email`

### Issue: "Migration failed: relation already exists"

**Solution:**
1. Drop database: `psql -U postgres -c "DROP DATABASE family_calendar_dev;"`
2. Recreate: `psql -U postgres -c "CREATE DATABASE family_calendar_dev;"`
3. Re-run migrations: `dotnet ef database update`

---

## Development Workflow

### Run with hot reload (file watcher)

```bash
dotnet watch run --project src/FamilyCalendar.Api
```

Changes to `.cs` files automatically trigger rebuild and restart.

### View logs

```bash
# Real-time logs
dotnet run --project src/FamilyCalendar.Api

# Verbose EF Core SQL logs
dotnet run --project src/FamilyCalendar.Api --verbosity detailed
```

### Reset database

```bash
# Drop and recreate
dotnet ef database drop --project src/FamilyCalendar.Api --force
dotnet ef database update --project src/FamilyCalendar.Api

# Re-create admin user
cd scripts && ./create-admin.sh --google-id "..." --email "..." --name "..."
```

---

## Next Steps

1. **Explore API Contracts**: Review OpenAPI specs in `specs/001-family-calendar/contracts/`
2. **Read Data Model**: Review entity relationships in `specs/001-family-calendar/data-model.md`
3. **Review Constitution**: Understand project principles in `.specify/memory/constitution.md`
4. **Start Development**: Run `/speckit.tasks` to generate implementation tasks

---

## Support

**Documentation:**
- Feature Spec: `specs/001-family-calendar/spec.md`
- Implementation Plan: `specs/001-family-calendar/plan.md`
- Research Decisions: `specs/001-family-calendar/research.md`

**Troubleshooting:**
- Check logs in terminal output
- Review `appsettings.Development.json` configuration
- Ensure all prerequisites installed with correct versions

**Tech Stack References:**
- .NET 8: https://learn.microsoft.com/en-us/aspnet/core
- EF Core: https://learn.microsoft.com/en-us/ef/core
- PostgreSQL: https://www.postgresql.org/docs/15/
- Google OAuth: https://developers.google.com/identity/protocols/oauth2
