using FluentValidation;
using MediatR;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation validators before the handler.
/// Returns ALL validation errors, not just the first one.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Validates the request using all registered validators before passing it to the handler.</summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        // If the response is a Result type, return all validation errors
        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var errorDetails = failures.Select(f =>
            {
                var key = f.ErrorMessage.StartsWith("lockey_")
                    ? f.ErrorMessage
                    : "lockey_validation_failed";

                var @params = new Dictionary<string, string>
                {
                    ["field"] = f.PropertyName
                };

                return new Error(new LocalizedMessage(key, @params));
            }).ToList();

            var primaryError = new Error(
                new LocalizedMessage("lockey_validation_failed"),
                errorDetails);

            var resultType = typeof(TResponse);
            var failureMethod = resultType.GetMethod("Failure",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                [typeof(Error)]);

            if (failureMethod is not null)
                return (TResponse)failureMethod.Invoke(null, [primaryError])!;
        }

        throw new ValidationException(failures);
    }
}
