# Debugging Guide - Family Calendar API

## Quick Start - Recommended Method

### Option 1: Debug Locally (Outside Docker) ⭐ EASIEST

1. **Stop the Docker API container** (keep PostgreSQL running):
   ```bash
   docker stop family-calendar-api
   ```

2. **Set breakpoints** in VS Code (e.g., in `InvitationsController.cs`)

3. **Press F5** or go to Run & Debug panel and select **".NET Core Launch (web)"**

4. The API will start at `http://localhost:5000` with debugger attached

5. **Open Swagger**: http://localhost:5000/swagger

6. **Test endpoints** and hit your breakpoints!

7. **When done**, restart Docker:
   ```bash
   docker start family-calendar-api
   ```

---

## Other Debugging Options

### Option 2: Debug with Hot Reload

Run with file watching (automatically recompiles on changes):
```bash
cd src/FamilyCalendar.Api
dotnet watch run
```

Then attach debugger:
- Press `Ctrl+Shift+P` → "Debug: Attach to Process"
- Select the `FamilyCalendar.Api` process

### Option 3: Debug Inside Docker Container

1. **Modify Dockerfile** to include debugger (already configured if using multi-stage build)

2. **Rebuild with debug configuration**:
   ```bash
   docker-compose -f docker-compose.debug.yml up --build
   ```

3. **In VS Code**, select **"Attach to Docker"** from Run & Debug

4. Choose the running container process

---

## Environment Setup

### Check Database Connection

PostgreSQL is running in Docker on `localhost:5432`:
```bash
# Test connection
docker exec -it family-calendar-postgres psql -U postgres -d familycalendar
```

### Environment Variables

The app reads from `appsettings.Local.json` (gitignored) or `appsettings.Development.json`.

Create `src/FamilyCalendar.Api/appsettings.Local.json` for local settings:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=familycalendar;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "Secret": "your-super-secret-jwt-key-minimum-32-characters-long",
    "Issuer": "http://localhost:5000",
    "Audience": "http://localhost:5000",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "GoogleOAuth": {
    "ClientId": "your-google-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-google-client-secret"
  }
}
```

---

## Debugging Tips

### Set Breakpoints
- Click left margin in code editor or press `F9`
- Breakpoints show as red dots
- Right-click for conditional breakpoints

### Keyboard Shortcuts
- **F5**: Start debugging
- **F10**: Step over
- **F11**: Step into
- **Shift+F11**: Step out
- **Shift+F5**: Stop debugging
- **Ctrl+Shift+F5**: Restart debugging

### Debug Console
- View variables: Hover over code or use Variables panel
- Watch expressions: Add to Watch panel
- Evaluate expressions: Use Debug Console (`` Ctrl+` ``)

### Logging
All logs appear in:
- VS Code Debug Console
- Terminal output
- Docker logs: `docker logs family-calendar-api -f`

### Common Issues

**Port 5000 already in use:**
```bash
# Find process using port 5000
netstat -ano | findstr :5000

# Kill the process or stop Docker container
docker stop family-calendar-api
```

**Database connection failed:**
- Ensure PostgreSQL container is running: `docker ps`
- Check connection string in `appsettings.Local.json`
- Verify port 5432 is accessible

**Breakpoints not hitting:**
- Ensure you're running in Debug configuration (not Release)
- Check you've built the project: `dotnet build`
- Verify the correct process is attached

---

## Testing Endpoints

### Using Bruno (Recommended)
1. Open Bruno collection: `integration/testing/bruno/KulaApp`
2. Set environment variables
3. Run requests with debugger active

### Using Swagger UI
1. Navigate to http://localhost:5000/swagger
2. Authenticate first (use Auth endpoints)
3. Test invitation endpoints with debugger active

### Using curl
```bash
# Example: Create invitation
curl -X POST http://localhost:5000/api/v1/tenants/{tenantId}/invitations \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"invitedEmail": "test@example.com"}'
```

---

## VS Code Extensions (Recommended)

- **C# Dev Kit** - Enhanced C# support
- **C#** - Base C# support
- **.NET Install Tool** - Manage .NET SDKs
- **REST Client** - Test APIs directly in VS Code
- **Docker** - Container management

Install via: `Ctrl+Shift+X` → Search → Install
