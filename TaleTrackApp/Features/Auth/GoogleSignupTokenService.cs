using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaleTrackApp.Features.Auth;

/// <summary>
/// Issues and validates a short-lived (15 minutes), stateless token that carries a verified Google
/// identity (id and email) between "Google confirmed who you are" and "you picked a username".
/// No user row exists yet at that point, so it cannot be an <see cref="Model.AuthActionToken"/>
/// (which is always tied to an existing user).
/// The token is a JWT signed with the same secret as the access tokens, but it never carries a
/// <c>sub</c>/NameIdentifier claim. Protected endpoints take the caller's id from that claim, so they
/// reject it even if someone sends it as a bearer token.
/// </summary>
public class GoogleSignupTokenService
{
    /// <summary>Value of the <c>purpose</c> claim that marks a token as a pending Google sign-up.</summary>
    private const string Purpose = "google_signup";

    /// <summary>How long the user has to pick a username.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>Application configuration (the <c>JwtSettings</c> section).</summary>
    private readonly IConfiguration _configuration;

    /// <summary>Creates the service.</summary>
    /// <param name="configuration">Source of the <c>JwtSettings</c> values (secret, issuer, audience).</param>
    public GoogleSignupTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Creates a pending sign-up token for a Google identity that has no account yet.</summary>
    /// <param name="googleId">Google account id (<c>sub</c> of Google's id token).</param>
    /// <param name="email">Email of the Google account.</param>
    /// <returns>The signed token.</returns>
    /// <exception cref="InvalidOperationException">The JWT secret is not configured.</exception>
    public string Issue(string googleId, string email)
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
    /// <param name="token">Token previously returned by <see cref="Issue"/>.</param>
    /// <returns>The Google identity carried by the token, or null.</returns>
    public (string GoogleId, string Email)? Validate(string token)
    {
        var jwtSecret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrEmpty(jwtSecret)) return null;

        // By default JwtSecurityTokenHandler rewrites short claim names (like "email") to long
        // XML/SOAP claim URIs on validation, and the literal "email" lookup below would never match.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
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
        if (string.IsNullOrWhiteSpace(googleId) || string.IsNullOrWhiteSpace(email))
            return null;

        return (googleId, email);
    }
}
