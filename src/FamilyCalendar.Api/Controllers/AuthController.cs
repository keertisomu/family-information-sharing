using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;
using FamilyCalendar.Api.Data;
using FamilyCalendar.Api.Models;
using FamilyCalendar.Api.Services;

namespace FamilyCalendar.Api.Controllers;

/// <summary>
/// Authentication and authorization endpoints for Google OAuth and JWT token management
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public AuthController(
        ILogger<AuthController> logger,
        ApplicationDbContext context,
        JwtTokenService jwtTokenService,
        IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates Google OAuth authentication flow by redirecting to Google's consent screen
    /// </summary>
    /// <remarks>
    /// This endpoint cannot be tested directly from Swagger UI as it returns a 302 redirect.
    /// Open this URL directly in your browser: GET /api/v1/auth/google/login
    /// </remarks>
    /// <returns>Redirect to Google OAuth authorization URL</returns>
    /// <response code="302">Redirects to Google OAuth consent screen</response>
    [HttpGet("google/login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult GoogleLogin()
    {
        var clientId = _configuration["Google:ClientId"];
        // Use localhost consistently to match Google Cloud Console configuration
        var redirectUri = $"{Request.Scheme}://localhost:5000/api/v1/auth/google/callback";
        var scope = "openid profile email";
        var state = Guid.NewGuid().ToString(); // CSRF protection
        
        var googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
            $"client_id={clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(scope)}&" +
            $"state={state}";

        _logger.LogInformation("Redirecting to Google OAuth for authentication");
        return Redirect(googleAuthUrl);
    }

    /// <summary>
    /// Handles Google OAuth callback, validates token, and issues JWT access tokens
    /// </summary>
    /// <param name="code">Authorization code from Google</param>
    /// <param name="state">CSRF protection state parameter</param>
    /// <returns>Authentication response with JWT tokens and user information</returns>
    /// <response code="200">Successfully authenticated and returned JWT tokens</response>
    /// <response code="400">Missing authorization code</response>
    /// <response code="401">Invalid or expired authorization code/token</response>
    /// <response code="500">Internal server error during authentication</response>
    [HttpGet("google/callback")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> GoogleCallback([FromQuery] string code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("Google OAuth callback received without authorization code");
            return BadRequest(new ErrorResponse
            {
                StatusCode = 400,
                Error = "Bad Request",
                Message = "Authorization code is required"
            });
        }

        try
        {
            // Exchange authorization code for access token
            var tokenRequest = new
            {
                code,
                client_id = _configuration["Google:ClientId"],
                client_secret = _configuration["Google:ClientSecret"],
                redirect_uri = $"{Request.Scheme}://localhost:5000/api/v1/auth/google/callback",
                grant_type = "authorization_code"
            };

            using var httpClient = new HttpClient();
            var tokenResponse = await httpClient.PostAsJsonAsync("https://oauth2.googleapis.com/token", tokenRequest);
            
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to exchange authorization code for token");
                return Unauthorized(new ErrorResponse
                {
                    StatusCode = 401,
                    Error = "Unauthorized",
                    Message = "Invalid or expired authorization code"
                });
            }

            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>();
            if (tokenData?.id_token == null)
            {
                _logger.LogError("Token response did not contain id_token");
                return Unauthorized(new ErrorResponse
                {
                    StatusCode = 401,
                    Error = "Unauthorized",
                    Message = "Invalid token response from Google"
                });
            }

            // Validate Google ID token
            var payload = await GoogleJsonWebSignature.ValidateAsync(tokenData.id_token);
            
            // Find or create user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);
            
            if (user == null)
            {
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    Name = payload.Name,
                    IsGlobalAdmin = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.Users.Add(user);
                _logger.LogInformation("Creating new user with email: {Email}", user.Email);
            }
            else
            {
                user.Email = payload.Email;
                user.Name = payload.Name;
                user.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updating existing user: {UserId}", user.UserId);
            }
            
            await _context.SaveChangesAsync();

            // Get user's tenants
            var tenantMemberships = await _context.TenantMembers
                .Include(tm => tm.Tenant)
                .Where(tm => tm.UserId == user.UserId)
                .ToListAsync();

            // Generate JWT token (with first tenant if available)
            var primaryTenantId = tenantMemberships.FirstOrDefault()?.TenantId;
            var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Email, primaryTenantId, user.IsGlobalAdmin);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            var response = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new UserInfo
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    Name = user.Name,
                    IsGlobalAdmin = user.IsGlobalAdmin
                },
                Tenant = tenantMemberships.Select(tm => new TenantInfo
                {
                    TenantId = tm.TenantId,
                    Name = tm.Tenant.Name,
                    Role = tm.Role.ToString().ToLower()
                }).FirstOrDefault()
            };

            _logger.LogInformation("User {UserId} authenticated successfully with {TenantCount} tenants", 
                user.UserId, tenantMemberships.Count);
            
            return Ok(response);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogError(ex, "Invalid JWT token from Google");
            return Unauthorized(new ErrorResponse
            {
                StatusCode = 401,
                Error = "Unauthorized",
                Message = "Invalid Google authentication token"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google OAuth callback");
            return StatusCode(500, new ErrorResponse
            {
                StatusCode = 500,
                Error = "Internal Server Error",
                Message = "An error occurred during authentication"
            });
        }
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token
    /// </summary>
    /// <param name="request">Refresh token request containing the refresh token</param>
    /// <returns>New authentication response with refreshed tokens</returns>
    /// <response code="200">Successfully refreshed tokens</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation("Token refresh requested");
        
        // TODO: Implement proper refresh token validation and rotation
        // For now, return a placeholder response
        return Ok(new AuthResponse
        {
            AccessToken = "new-access-token-placeholder",
            RefreshToken = "new-refresh-token-placeholder",
            User = new UserInfo
            {
                UserId = Guid.NewGuid(),
                Email = "placeholder@example.com",
                Name = "Placeholder User",
                IsGlobalAdmin = false
            },
            Tenant = null
        });
    }

    /// <summary>
    /// Switches the authenticated user's active tenant context
    /// </summary>
    /// <param name="request">Request containing the target tenant ID</param>
    /// <returns>New authentication response with updated tenant context</returns>
    /// <response code="200">Successfully switched tenant context</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="403">User is not a member of the requested tenant</response>
    [HttpPost("select-tenant")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> SelectTenant([FromBody] SelectTenantRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                StatusCode = 401,
                Error = "Unauthorized",
                Message = "User not authenticated"
            });
        }

        // Verify user is a member of the requested tenant
        var membership = await _context.TenantMembers
            .Include(tm => tm.Tenant)
            .Include(tm => tm.User)
            .FirstOrDefaultAsync(tm => tm.UserId == userId.Value && tm.TenantId == request.TenantId);

        if (membership == null)
        {
            _logger.LogWarning("User {UserId} attempted to select tenant {TenantId} without membership", 
                userId.Value, request.TenantId);
            return Forbid();
        }

        // Generate new JWT with selected tenant
        var accessToken = _jwtTokenService.GenerateAccessToken(membership.User.UserId, membership.User.Email, request.TenantId, membership.User.IsGlobalAdmin);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserInfo
            {
                UserId = membership.User.UserId,
                Email = membership.User.Email,
                Name = membership.User.Name,
                IsGlobalAdmin = membership.User.IsGlobalAdmin
            },
            Tenant = new TenantInfo
            {
                TenantId = membership.TenantId,
                Name = membership.Tenant.Name,
                Role = membership.Role.ToString().ToLower()
            }
        };

        _logger.LogInformation("User {UserId} switched to tenant {TenantId}", userId.Value, request.TenantId);
        return Ok(response);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }
        return userId;
    }
}

public class GoogleTokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string id_token { get; set; } = string.Empty;
    public int expires_in { get; set; }
    public string token_type { get; set; } = string.Empty;
    public string? refresh_token { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class SelectTenantRequest
{
    public Guid TenantId { get; set; }
}
