using TaleTrackApp.OpenApi;
using TaleTrackApp.Security;
using TaleTrackApp.Features.User;

namespace TaleTrackApp.Features.Auth.VerifyCode;

/// <summary>
/// <c>POST /api/auth/verify-code</c>: redeems a code from <see cref="RequestCode.RequestCodeEndpoint"/>
/// for a session, labelled with the request's device ("KOReader" by default). Anonymous. Returns 401
/// with code <c>invalid_code</c> if the code is wrong, expired or discarded after five wrong guesses.
/// </summary>
public static class VerifyCodeEndpoint
{
    /// <summary>Registers the endpoint on the <c>/api</c> route group.</summary>
    /// <param name="group">The <c>/api</c> group.</param>
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/auth/verify-code", HandleAsync)
            .WithName("VerifyEmailCode")
            .WithTags("Auth")
            .WithSummary("Sign in with an emailed code")
            .WithDescription("Redeems the code from `POST /api/auth/request-code` for a session. `device` labels the session in the sessions list and defaults to `KOReader`. The six-digit code is valid for 10 minutes and works once; five wrong guesses discard it and a new one must be requested.")
            .Responds<VerifyCodeResponse>("Signed in.")
            .RespondsBadRequest("Validation failed: the message lists every violated rule.")
            .Responds<ApiError>(StatusCodes.Status401Unauthorized, "Wrong, expired or discarded code (five wrong guesses), with code `invalid_code`.")
            .AddEndpointFilter<ValidationFilter>()
            .AllowAnonymous();
    }

    /// <summary>Sign in with an emailed code</summary>
    private static async Task<IResult> HandleAsync(
        VerifyCodeRequest request,
        UserService userService,
        SessionService sessions,
        ILogger<VerifyCodeRequest> logger)
    {
        var user = await userService.VerifyEmailCodeAsync(request.Email, request.Code);
        if (user == null)
            return Results.Json(new ApiError { Success = false, Code = "invalid_code", Message = "The code is wrong or has expired." }, statusCode: 401);

        var device = string.IsNullOrWhiteSpace(request.Device) ? "KOReader" : request.Device;
        var session = await sessions.StartAsync(user, device);
        logger.LogInformation("User {Username} authenticated via email code", user.Username);

        return Results.Ok(new VerifyCodeResponse
        {
            Success = true,
            Message = "Verification successful",
            Token = session.AccessToken,
            RefreshToken = session.RefreshToken,
            ExpiresIn = session.ExpiresIn,
        });
    }
}
