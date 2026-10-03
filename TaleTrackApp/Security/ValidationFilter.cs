using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Security;

/// <summary>
/// Endpoint filter that checks the DataAnnotations (<c>[Required]</c>, <c>[StringLength]</c>, ...) of
/// every argument bound to the handler before it runs. Endpoints opt in with
/// <c>AddEndpointFilter&lt;ValidationFilter&gt;()</c>.
/// </summary>
public class ValidationFilter : IEndpointFilter
{
    /// <summary>
    /// Validates each non-null argument. If any rule fails, the handler is skipped and the
    /// response is a <c>400</c> whose <c>message</c> joins every violated rule with "; ".
    /// </summary>
    /// <param name="context">The arguments the endpoint is about to be called with.</param>
    /// <param name="next">The rest of the pipeline, ending in the handler.</param>
    /// <returns>The <c>400</c> result, or whatever the handler returns.</returns>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null) continue;

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(argument);

            if (!Validator.TryValidateObject(argument, validationContext, validationResults, validateAllProperties: true))
            {
                var errors = validationResults
                    .Select(vr => vr.ErrorMessage)
                    .Where(msg => !string.IsNullOrEmpty(msg))
                    .ToList();

                return Results.BadRequest(new { message = string.Join("; ", errors) });
            }
        }

        return await next(context);
    }
}
