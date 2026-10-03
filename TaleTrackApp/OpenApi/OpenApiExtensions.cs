namespace TaleTrackApp.OpenApi;

/// <summary>
/// Documents one response of an endpoint: its status code and what it means. Attached as endpoint
/// metadata by <see cref="OpenApiExtensions"/> and read by <see cref="ResponsesOperationFilter"/>.
/// </summary>
/// <param name="StatusCode">HTTP status code of the response.</param>
/// <param name="Text">Description shown for that response in the OpenAPI document.</param>
public sealed record ResponseDescription(int StatusCode, string Text);

/// <summary>
/// Shorthands used in each endpoint's <c>Map</c> to declare a response (status code, body type and
/// description) in a single call.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>Documents a response that carries a body of type <typeparamref name="TBody"/>.</summary>
    /// <typeparam name="TBody">Type of the response body.</typeparam>
    /// <param name="builder">The endpoint being mapped.</param>
    /// <param name="statusCode">HTTP status code of the response.</param>
    /// <param name="description">What the response means.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder Responds<TBody>(
        this RouteHandlerBuilder builder, int statusCode, string description) =>
        builder.Produces<TBody>(statusCode).WithMetadata(new ResponseDescription(statusCode, description));

    /// <summary>Documents a <c>200</c> response that carries a body of type <typeparamref name="TBody"/>.</summary>
    /// <typeparam name="TBody">Type of the response body.</typeparam>
    /// <param name="builder">The endpoint being mapped.</param>
    /// <param name="description">What the response means.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder Responds<TBody>(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<TBody>(StatusCodes.Status200OK, description);

    /// <summary>Documents a response without a body.</summary>
    /// <param name="builder">The endpoint being mapped.</param>
    /// <param name="statusCode">HTTP status code of the response.</param>
    /// <param name="description">What the response means.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder Responds(
        this RouteHandlerBuilder builder, int statusCode, string description) =>
        builder.Produces(statusCode).WithMetadata(new ResponseDescription(statusCode, description));

    /// <summary>Documents a <c>400</c> whose body is an <see cref="ApiError"/>.</summary>
    /// <param name="builder">The endpoint being mapped.</param>
    /// <param name="description">When the endpoint answers <c>400</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder RespondsBadRequest(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<ApiError>(StatusCodes.Status400BadRequest, description);

    /// <summary>Documents a <c>404</c> whose body is an <see cref="ApiError"/>.</summary>
    /// <param name="builder">The endpoint being mapped.</param>
    /// <param name="description">When the endpoint answers <c>404</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder RespondsNotFound(this RouteHandlerBuilder builder, string description) =>
        builder.Responds<ApiError>(StatusCodes.Status404NotFound, description);
}
