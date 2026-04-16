using System.ComponentModel.DataAnnotations;

namespace TaleTrackApp.Auth;

public class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Validate all arguments
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
