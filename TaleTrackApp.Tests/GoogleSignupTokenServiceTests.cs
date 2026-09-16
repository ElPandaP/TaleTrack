using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaleTrackApp.Features.User.GoogleLogin;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Pure unit tests for the pending-Google-signup token: no HTTP, no database — just the
/// service and an in-memory IConfiguration standing in for appsettings/env. Covers the one
/// piece of standalone, non-trivial logic in the Google sign-up flow that the integration
/// suite cannot easily reach, since it depends on a real Google id_token to get past
/// GoogleLoginEndpoint.
/// </summary>
public class GoogleSignupTokenServiceTests
{
    private const string Secret = "unit-test-secret-at-least-32-bytes-long!!";
    private const string Issuer = "TaleTrackApp";
    private const string Audience = "TaleTrackApp";

    private static GoogleSignupTokenService NewService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = Secret,
                ["JwtSettings:Issuer"] = Issuer,
                ["JwtSettings:Audience"] = Audience,
            })
            .Build();
        return new GoogleSignupTokenService(config);
    }

    private static string BuildRawToken(TimeSpan expiresIn, string purpose = "google_signup", string googleId = "g-1", string email = "a@b.com", string name = "A B")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("purpose", purpose),
            new Claim("google_id", googleId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("name", name),
        };
        var token = new JwtSecurityToken(Issuer, Audience, claims,
            expires: DateTime.UtcNow.Add(expiresIn), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void IssueThenValidate_RoundTripsTheSameIdentity()
    {
        var service = NewService();

        var token = service.Issue("google-42", "user@example.com", "Jane Doe");
        var identity = service.Validate(token);

        Assert.NotNull(identity);
        Assert.Equal("google-42", identity!.Value.GoogleId);
        Assert.Equal("user@example.com", identity.Value.Email);
        Assert.Equal("Jane Doe", identity.Value.Name);
    }

    [Fact]
    public void Validate_GarbageToken_ReturnsNull()
    {
        var service = NewService();

        Assert.Null(service.Validate("not-a-jwt-at-all"));
    }

    [Fact]
    public void Validate_TokenSignedWithADifferentSecret_ReturnsNull()
    {
        var service = NewService();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-secret-value!!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("purpose", "google_signup"),
            new Claim("google_id", "g-1"),
            new Claim(JwtRegisteredClaimNames.Email, "a@b.com"),
            new Claim("name", "A B"),
        };
        var forged = new JwtSecurityToken(Issuer, Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        var forgedToken = new JwtSecurityTokenHandler().WriteToken(forged);

        Assert.Null(service.Validate(forgedToken));
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsNull()
    {
        var service = NewService();
        var expired = BuildRawToken(TimeSpan.FromMinutes(-1));

        Assert.Null(service.Validate(expired));
    }

    [Fact]
    public void Validate_WrongPurposeClaim_ReturnsNull()
    {
        var service = NewService();
        var wrongPurpose = BuildRawToken(TimeSpan.FromMinutes(15), purpose: "password_reset");

        Assert.Null(service.Validate(wrongPurpose));
    }
}
