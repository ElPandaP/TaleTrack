using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Features.Auth;
using Xunit;

namespace TaleTrackApp.Tests;

/// <summary>
/// Signing up with email and password: the rules for the email, the username and the password,
/// the welcome email in the user's language, and the link in that email that deletes an account
/// someone created with an address that was not theirs.
/// </summary>
[Collection(ApiCollection.Name)]
public class RegistrationFlowTests(CustomWebApplicationFactory factory)
{
    private readonly CustomWebApplicationFactory _factory = factory;

    private Task<HttpResponseMessage> RegisterAsync(string email, string username, string password = CustomWebApplicationFactory.TestPassword, string? locale = null) =>
        _factory.CreateClient().PostAsJsonAsync("/api/register",
            new { Email = email, Username = username, Password = password, Locale = locale });

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = _factory.NewDbScope(out var db);
        return await db.Users.AnyAsync(u => u.Email == email);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesAnAccountThatCanLogIn()
    {
        var res = await RegisterAsync("signup-ok@test.com", "signupok");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/login",
            new { Email = "signup-ok@test.com", Password = CustomWebApplicationFactory.TestPassword });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("es", "Te damos la bienvenida")]
    [InlineData("en", "Welcome to TaleTrack")]
    public async Task Register_SendsTheWelcomeEmailInTheRequestLanguage(string locale, string subject)
    {
        var email = $"signup-welcome-{locale}@test.com";
        await RegisterAsync(email, $"signupwelcome{locale}", locale: locale);

        var welcome = await _factory.Resend.WaitForAsync(email, subject);
        Assert.NotNull(welcome.LinkToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // the same address in capitals
    public async Task Register_EmailAlreadyUsed_IsRejectedWithItsCode(bool otherCase)
    {
        var email = $"signup-taken-{otherCase}@test.com".ToLower();
        await _factory.CreateUserAsync(email, $"signuptaken{otherCase}");

        var res = await RegisterAsync(otherCase ? email.ToUpper() : email, $"signuptakenagain{otherCase}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("email_taken", await res.CodeAsync());
    }

    [Fact]
    public async Task Register_UsernameTakenInAnotherCase_IsRejectedWithItsCode()
    {
        await _factory.CreateUserAsync("signup-taken-name-1@test.com", "SignupTakenName");

        var res = await RegisterAsync("signup-taken-name-2@test.com", "signuptakenname");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("username_taken", await res.CodeAsync());
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]   // the minimum
    [InlineData(50, true)]  // the maximum
    [InlineData(51, false)]
    public async Task Register_UsernameLength_MustBeFrom3To50(int length, bool accepted)
    {
        var username = $"l{length}".PadRight(length, 'x');

        var res = await RegisterAsync($"signup-len-{length}@test.com", username);
        Assert.Equal(accepted, res.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("Short1A", false)]      // 7 characters
    [InlineData("Eight8ch", true)]      // 8 characters, the minimum
    [InlineData("alllower1", false)]    // no uppercase letter
    [InlineData("NoDigitsHere", false)] // no number
    public async Task Register_PasswordPolicy_IsEnforced(string password, bool accepted)
    {
        var res = await RegisterAsync($"signup-pw-{password}@test.com", $"signuppw{password}", password);
        Assert.Equal(accepted, res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task RevokeSignup_WithTheWelcomeLink_DeletesTheAccountAndItsData()
    {
        var user = await _factory.CreateUserAsync("signup-revoke@test.com", "signuprevoke");
        await user.Client.TrackMovieAsync("Revoked Signup Movie", 40);
        var welcome = await _factory.Resend.WaitForAsync("signup-revoke@test.com", "Welcome");

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/revoke-signup", new { Token = welcome.LinkToken });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        Assert.False(await UserExistsAsync("signup-revoke@test.com"));
        using var scope = _factory.NewDbScope(out var db);
        Assert.False(await db.TrackingEvents.AnyAsync(t => t.UserId == user.Id));
    }

    [Theory]
    [InlineData("used")]
    [InlineData("expired")]
    [InlineData("tampered")]
    [InlineData("other purpose")]
    public async Task RevokeSignup_WithAnInvalidLink_DeletesNothing(string problem)
    {
        var email = $"signup-badlink-{problem.Replace(' ', '-')}@test.com";
        var user = await _factory.CreateUserAsync(email, $"signupbad{problem.Replace(" ", "")}");
        var token = (await _factory.Resend.WaitForAsync(email, "Welcome")).LinkToken!;
        var client = _factory.CreateClient();

        switch (problem)
        {
            case "used":
                // A first use of the link would already delete the account, so the token is
                // marked consumed directly, as that first use would have left it.
                await _factory.UpdateActionTokenAsync(token, t => t.ConsumedAt = DateTime.UtcNow);
                break;
            case "expired":
                await _factory.UpdateActionTokenAsync(token, t => t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
                break;
            case "tampered":
                token += "x";
                break;
            case "other purpose":
                token = await _factory.IssueActionTokenAsync(user.Id, Model.AuthActionToken.PasswordReset);
                break;
        }

        var res = await client.PostAsJsonAsync("/api/auth/revoke-signup", new { Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True(await UserExistsAsync(email));
    }

    [Fact]
    public async Task Register_WhileTheEmailServiceIsDown_StillCreatesTheAccount()
    {
        var email = $"{FakeResend.DownMarker}-signup@test.com";

        var res = await RegisterAsync(email, "signupresenddown");
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.True(await UserExistsAsync(email));
    }
}
