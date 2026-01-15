# Google OAuth Setup Instructions

## Quick Setup

### 1. Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing one
3. Note your project name (kulaApp)

### 2. Enable Required APIs (Optional)

You can skip this step! OAuth 2.0 works without enabling additional APIs. 

However, if you want to fetch additional user profile information later, you can optionally enable:
- Search for "**People API**" and enable it (for accessing user profiles)

**Note**: Don't enable any of the "Identity Toolkit", "Cloud Identity", or "IAM" APIs - those are for different purposes.

### 3. Create OAuth 2.0 Credentials

1. Go to **APIs & Services** > **Credentials**
2. Click **+ CREATE CREDENTIALS** > **OAuth client ID**
3. If prompted, configure the OAuth consent screen first:
   - Choose "External" (for testing)
   - Fill in app name: "Family Calendar"
   - Add your email as developer contact
   - Click "Save and Continue"
   - Skip scopes (defaults are fine)
   - Add test users (your Gmail address)
   - Click "Save and Continue"

4. Create OAuth Client ID:
   - **Application type**: Web application
   - **Name**: Family Calendar API
   - **Authorized JavaScript origins**:
     - `http://localhost:5000`
     - `http://localhost:3000`
   - **Authorized redirect URIs**:
     - `http://localhost:5000/api/v1/auth/google/callback`
   - Click **Create**

5. **Copy the credentials**:
   - Client ID (ends with `.apps.googleusercontent.com`)
   - Client Secret

### 4. Configure Your Application

Edit `src/FamilyCalendar.Api/appsettings.Local.json`:

```json
{
  "Google": {
    "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "Jwt": {
    "SecretKey": "generate-a-random-32-character-string-here"
  }
}
```

### 5. Restart the Application

```bash
docker-compose down
docker-compose up -d
```

### 6. Test OAuth Flow

1. Open: http://localhost:5000/api/v1/auth/google/login
2. You should be redirected to Google's consent screen
3. Sign in with your Google account (must be a test user)
4. After consent, you'll be redirected back with a JWT token

## Troubleshooting

### "Error 401: invalid_client"
- Check that ClientId and ClientSecret are correctly copied
- Verify the OAuth client exists in Google Cloud Console

### "Error 400: redirect_uri_mismatch"
- The redirect URI in Google Cloud Console must exactly match:
  `http://localhost:5000/api/v1/auth/google/callback`
- No trailing slashes
- Check protocol is `http://` not `https://`

### "Error 403: access_denied"
- Add your Google account as a test user in OAuth consent screen
- Check the app is in "Testing" mode (allows test users)

### Production Deployment

For production:
1. Change OAuth consent screen to "Published"
2. Add production redirect URIs
3. Use environment variables for secrets (not appsettings files)
4. Use HTTPS for all redirect URIs
