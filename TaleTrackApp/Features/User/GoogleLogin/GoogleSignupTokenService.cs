using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaleTrackApp.Features.User.GoogleLogin;

/// <summary>
/// A short-lived, stateless token that carries a verified Google identity (id, email, name)
/// between "Google confirmed who you are" and "you picked a username" — no user row exists
/// yet, so this can't reuse AuthActionToken (which is always tied to an existing UserId).
/// Signed with the same secret as the real auth JWT, but never carries a `sub`/NameIdentifier
/// claim, so it can't be mistaken for (or misused as) a logged-in session by any [Authorize]
/// endpoint even if someone passed it as a bearer token.
/// </summary>
public class GoogleSignupTokenService
{
    private const string Purpose = "google_signup";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IConfiguration _configuration;

    public GoogleSignupTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Issue(string googleId, string email, string name)
    {
        var jwtSecret = _configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("purpose", Purpose),
            new Claim("google_id", googleId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", name),
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Validates signature, expiry and purpose. Returns null if anything is off.</summary>
    public (string GoogleId, string Email, string Name)? Validate(string token)
    {
        var jwtSecret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(jwtSecret)) return null;

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }

        if (principal.FindFirst("purpose")?.Value != Purpose) return null;

        var googleId = principal.FindFirst("google_id")?.Value;
        var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var name = principal.FindFirst("name")?.Value;
        if (string.IsNullOrWhiteSpace(googleId) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
            return null;

        return (googleId, email, name);
    }
}
