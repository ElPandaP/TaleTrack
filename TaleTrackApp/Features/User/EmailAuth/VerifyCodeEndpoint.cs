using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;
using TaleTrackApp.Features.Auth;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.User.EmailAuth;

public class VerifyCodeRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "The code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "The code must be 6 characters long")]
    public required string Code { get; set; }
}

public static class VerifyCodeEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/verify-code", HandleAsync)
            .WithName("VerifyEmailCode")
            .WithDescription("Verifies the email code and returns a JWT")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        VerifyCodeRequest request,
        UserService userService,
        JwtService jwtService,
        RefreshTokenService refreshTokens,
        ILogger<VerifyCodeRequest> logger)
    {
        var user = await userService.GetByEmailAsync(request.Email);
        if (user == null || user.EmailCode == null || user.EmailCodeExpiry == null)
            return Results.Unauthorized();

        if (user.EmailCodeExpiry < DateTime.UtcNow)
        {
            logger.LogWarning("Expired email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        if (user.EmailCode != request.Code)
        {
            logger.LogWarning("Invalid email code attempt for {Email}", request.Email);
            return Results.Unauthorized();
        }

        await userService.ClearEmailCodeAsync(user.Id);

        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, "Web");
        logger.LogInformation("User {Username} authenticated via email code", user.Username);

        return Results.Ok(new
        {
            success = true,
            message = "Verification successful",
            token,
            refreshToken,
            expiresIn = jwtService.ExpirationMinutes * 60,
        });
    }
}
