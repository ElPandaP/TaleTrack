using System.ComponentModel.DataAnnotations;
using TaleTrackApp.Auth;

namespace TaleTrackApp.Features.Auth.Refresh;

public class RefreshRequest
{
    [Required(ErrorMessage = "refreshToken is required")]
    public required string RefreshToken { get; set; }
}

public static class RefreshEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/refresh", HandleAsync)
            .WithName("RefreshToken")
            .WithDescription("Exchanges a valid refresh token for a new access + refresh token pair")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        RefreshRequest request,
        RefreshTokenService refreshTokens,
        JwtService jwtService,
        ILogger<RefreshRequest> logger)
    {
        var rotated = await refreshTokens.ValidateAndRotateAsync(request.RefreshToken);
        if (rotated is null)
            return Results.Json(new { success = false, message = "Invalid or expired refresh token." }, statusCode: 401);

        var (user, newRefreshToken) = rotated.Value;
        var token = jwtService.GenerateToken(user.Id, user.Email, user.Username);

        logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);

        return Results.Ok(new
        {
            success = true,
            token,
            refreshToken = newRefreshToken,
            expiresIn = jwtService.ExpirationMinutes * 60,
        });
    }
}
