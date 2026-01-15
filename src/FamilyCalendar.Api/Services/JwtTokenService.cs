using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FamilyCalendar.Api.Services;

/// <summary>
/// Service for generating JWT access tokens and refresh tokens.
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates a JWT access token for a user with the specified claims.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="tenantId">The tenant ID (optional for initial login).</param>
    /// <param name="isGlobalAdmin">Whether the user is a global administrator.</param>
    /// <returns>The generated JWT access token as a string.</returns>
    public string GenerateAccessToken(Guid userId, string email, Guid? tenantId, bool isGlobalAdmin)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("is_global_admin", isGlobalAdmin.ToString().ToLower())
        };

        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured")));
        
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1), // Access token valid for 1 hour
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        _logger.LogInformation(
            "Generated access token for user {UserId} with tenant {TenantId}", 
            userId, 
            tenantId?.ToString() ?? "none");

        return tokenString;
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    /// <returns>A base64-encoded refresh token string.</returns>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        var refreshToken = Convert.ToBase64String(randomBytes);
        
        _logger.LogDebug("Generated new refresh token");
        
        return refreshToken;
    }
}
