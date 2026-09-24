namespace TaleTrackApp.OpenApi;

/// <summary>Documents one response of an endpoint: its status code and what it means.</summary>
public sealed record ResponseDescription(int StatusCode, string Text);

public static class OpenApiExtensions
{
    /// <summary>Documents a response that carries a body of type <typeparamref name="TBody"/>.</summary>
    public static RouteHandlerBuilder Responds<TBody>(
        this RouteHandlerBuilder builder, int statusCode, string description) =>
        builder.Produces<TBody>(statusCode).WithMetadata(new ResponseDescription(statusCode, description));

    /// <summary>Documents a <c>200</c> response that carries a body of type <typeparamref name="TBody"/>.</summary>
    public static RouteHandlerBuilder Responds<TBody>(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<TBody>(StatusCodes.Status200OK, description);

    /// <summary>Documents a response without a body.</summary>
    public static RouteHandlerBuilder Responds(
        this RouteHandlerBuilder builder, int statusCode, string description) =>
        builder.Produces(statusCode).WithMetadata(new ResponseDescription(statusCode, description));

    /// <summary>Documents a <c>400</c> whose body is an <see cref="ApiError"/>.</summary>
    public static RouteHandlerBuilder RespondsBadRequest(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<ApiError>(StatusCodes.Status400BadRequest, description);

    /// <summary>Documents a <c>404</c> whose body is an <see cref="ApiError"/>.</summary>
    public static RouteHandlerBuilder RespondsNotFound(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<ApiError>(StatusCodes.Status404NotFound, description);
}
