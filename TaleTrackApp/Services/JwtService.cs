using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaleTrackApp.Services;

/// <summary>
/// Creates the signed access tokens (JWT) that authenticate every request. Issuer, audience,
/// lifetime and secret come from the <c>JwtSettings</c> configuration section.
/// </summary>
public class JwtService
{
    /// <summary>Application configuration (the <c>JwtSettings</c> section).</summary>
    private readonly IConfiguration _configuration;
    /// <summary>Logger.</summary>
    private readonly ILogger<JwtService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="configuration">Source of the <c>JwtSettings</c> values.</param>
    /// <param name="logger">Logs every issued token.</param>
    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Lifetime of a freshly issued access token, in minutes.</summary>
    public int ExpirationMinutes =>
        int.TryParse(_configuration["JwtSettings:ExpirationMinutes"], out var m) ? m : 60;

    /// <summary>
    /// Issues an access token for a user. It carries the user id (<c>sub</c> and
    /// <see cref="ClaimTypes.NameIdentifier"/>), email, username and a unique id (<c>jti</c>), and
    /// expires after <see cref="ExpirationMinutes"/>.
    /// </summary>
    /// <param name="userId">Id of the user the token authenticates.</param>
    /// <param name="email">The user's email.</param>
    /// <param name="username">The user's username.</param>
    /// <returns>The serialized, signed token.</returns>
    /// <exception cref="InvalidOperationException">The JWT secret is not configured.</exception>
    public string GenerateToken(Guid userId, string email, string username)
    {
        var jwtSecret = _configuration["JwtSettings:Secret"];
        
        if (string.IsNullOrEmpty(jwtSecret))
        {
            _logger.LogError("JWT Secret not configured!");
            throw new InvalidOperationException("JWT Secret is not configured");
        }

        var jwtIssuer = _configuration["JwtSettings:Issuer"];
        var jwtAudience = _configuration["JwtSettings:Audience"];
        var jwtExpirationMinutes = ExpirationMinutes;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        
        _logger.LogDebug("JWT issued for user {Username} ({UserId})", username, userId);
        
        return tokenString;
    }
}
