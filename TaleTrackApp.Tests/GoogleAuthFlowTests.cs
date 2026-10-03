using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Signing in and signing up with Google: an identity already linked, an existing password
/// account that gets Google linked by email, and a new account, which is only created once the
/// user picks a username with the pending sign-up token. Google's validation of the id token is
/// replaced by <see cref="FakeGoogleIdTokenValidator"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public class GoogleAuthFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private async Task<JsonElement> GoogleLoginAsync(string googleId, string email)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google",
            new { IdToken = FakeGoogleIdTokenValidator.Token(googleId, email) });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> CompleteAsync(string? pendingToken, string username) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/google/complete",
            new { PendingToken = pendingToken, Username = username, Locale = "en" });

    private async Task<string> UsernameOfAsync(HttpResponseMessage signedIn)
    {
        var token = (await signedIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/users/me");
        return me.GetProperty("data").GetProperty("username").GetString()!;
    }

    private async Task<int> AccountsWithEmailAsync(string email)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Users.CountAsync(u => u.Email == email);
    }

    [Fact]
    public async Task GoogleLogin_IdentityAlreadyLinked_SignsIn()
    {
        var pending = (await GoogleLoginAsync("google-linked", "google-linked@test.com")).GetProperty("pendingToken").GetString();
        Assert.True((await CompleteAsync(pending, "googlelinked")).IsSuccessStatusCode);

        var again = await GoogleLoginAsync("google-linked", "google-linked@test.com");
        Assert.False(string.IsNullOrWhiteSpace(again.GetProperty("token").GetString()));
        Assert.False(again.GetProperty("linkedExistingAccount").GetBoolean());
    }

    [Fact]
    public async Task GoogleLogin_EmailOfAPasswordAccount_LinksGoogle_AndThePasswordStillWorks()
    {
        await _factory.CreateUserAsync("google-link-email@test.com", "googlelinkemail");

        var body = await GoogleLoginAsync("google-link-email", "google-link-email@test.com");
        Assert.True(body.GetProperty("linkedExistingAccount").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/login",
            new { Email = "google-link-email@test.com", Password = CustomWebApplicationFactory.TestPassword });
        Assert.True(login.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GoogleLogin_UnknownIdentity_AsksForAUsername_AndCreatesNothingYet()
    {
        var body = await GoogleLoginAsync("google-new", "google-new@test.com");

        Assert.True(body.GetProperty("needsUsername").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("pendingToken").GetString()));
        Assert.Equal(0, await AccountsWithEmailAsync("google-new@test.com"));
    }

    [Fact]
    public async Task CompleteSignup_CreatesTheAccount_SendsTheWelcomeEmail_AndSignsIn()
    {
        var pending = (await GoogleLoginAsync("google-complete", "google-complete@test.com")).GetProperty("pendingToken").GetString();

        var res = await CompleteAsync(pending, "googlecomplete");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("googlecomplete", await UsernameOfAsync(res));
        await _factory.Resend.WaitForAsync("google-complete@test.com", "Welcome");
    }

    [Fact]
    public async Task CompleteSignup_UsernameTaken_AsksForAnother_WithTheSameToken()
    {
        await _factory.CreateUserAsync("google-taken-owner@test.com", "googletakenname");
        var pending = (await GoogleLoginAsync("google-taken", "google-taken@test.com")).GetProperty("pendingToken").GetString();

        var taken = await CompleteAsync(pending, "GoogleTakenName");
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal("username_taken", await taken.CodeAsync());

        var retry = await CompleteAsync(pending, "googletakenretry");
        Assert.Equal("googletakenretry", await UsernameOfAsync(retry));
    }

    [Fact]
    public async Task CompleteSignup_SubmittedTwice_CreatesOneAccount()
    {
        var pending = (await GoogleLoginAsync("google-twice", "google-twice@test.com")).GetProperty("pendingToken").GetString();

        await CompleteAsync(pending, "googletwice");
        var again = await CompleteAsync(pending, "googletwiceb");

        Assert.Equal("googletwice", await UsernameOfAsync(again));
        Assert.Equal(1, await AccountsWithEmailAsync("google-twice@test.com"));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("signed with another key")]
    [InlineData("other purpose")]
    [InlineData("corrupt")]
    public async Task CompleteSignup_WithAnInvalidPendingToken_CreatesNothing(string problem)
    {
        var email = $"google-bad-{problem.Replace(' ', '-')}@test.com";
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var token = problem switch
        {
            "expired" => PendingToken(config, email, DateTime.UtcNow.AddMinutes(-5), config["JwtSettings:Secret"]!, "google_signup"),
            "signed with another key" => PendingToken(config, email, DateTime.UtcNow.AddMinutes(10), "another-secret-key-with-at-least-32-chars!!", "google_signup"),
            "other purpose" => PendingToken(config, email, DateTime.UtcNow.AddMinutes(10), config["JwtSettings:Secret"]!, "password_reset"),
            _ => "not-a-token",
        };

        var res = await CompleteAsync(token, $"googlebad{problem.Replace(" ", "")}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("invalid_or_expired", await res.CodeAsync());
        Assert.Equal(0, await AccountsWithEmailAsync(email));
    }

    [Fact]
    public async Task GoogleLogin_WithAnIdTokenGoogleRejects_DoesNotSignIn()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/google", new { IdToken = "forged-id-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("invalid_google_token", await res.CodeAsync());
    }

    /// <summary>
    /// Builds a pending sign-up token the way the app does, but with the expiry, key and purpose
    /// chosen by the test, to check that the app rejects each kind of bad token.
    /// </summary>
    private static string PendingToken(IConfiguration config, string email, DateTime expires, string secret, string purpose)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["JwtSettings:Issuer"],
            audience: config["JwtSettings:Audience"],
            claims: [new Claim("purpose", purpose), new Claim("google_id", $"id-{email}"), new Claim(JwtRegisteredClaimNames.Email, email)],
            notBefore: expires.AddMinutes(-15),
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
