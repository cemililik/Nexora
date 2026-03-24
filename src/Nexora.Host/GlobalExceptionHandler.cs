using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Host;

/// <summary>
/// Catches all unhandled exceptions and returns a standardized ApiEnvelope error response.
/// Maps exception types to appropriate HTTP status codes and lockey_ error keys.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorKey, logLevel, errorParams) = MapException(exception);
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        logger.Log(logLevel, exception,
            "Unhandled {ExceptionType} at {Method} {Path}, TraceId: {TraceId}",
            exception.GetType().Name,
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        httpContext.Response.StatusCode = statusCode;

        if (exception is ValidationException validationException)
        {
            var validationErrors = validationException.Errors
                .Select(e => new ApiValidationError(
                    e.ErrorMessage.StartsWith("lockey_") ? e.ErrorMessage : "lockey_validation_failed",
                    new Dictionary<string, string> { ["field"] = e.PropertyName }))
                .ToList();

            var validationEnvelope = ApiEnvelope<object>.ValidationFail(validationErrors, traceId);
            await httpContext.Response.WriteAsJsonAsync(validationEnvelope, cancellationToken);
            return true;
        }

        var envelope = ApiEnvelope<object>.Fail(
            new Error(new LocalizedMessage(errorKey, errorParams)), traceId);

        await httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
        return true;
    }

    private static (int StatusCode, string ErrorKey, LogLevel LogLevel, Dictionary<string, string>? Params)
        MapException(Exception exception) => exception switch
    {
        DomainException domainEx =>
            (StatusCodes.Status422UnprocessableEntity,
             domainEx.LocalizationKey,
             LogLevel.Warning,
             domainEx.Params.Count > 0 ? domainEx.Params : null),

        ValidationException =>
            (StatusCodes.Status400BadRequest,
             "lockey_error_validation_failed",
             LogLevel.Warning,
             null),

        OperationCanceledException =>
            (499, // Client Closed Request
             "lockey_error_request_cancelled",
             LogLevel.Debug,
             null),

        KeyNotFoundException =>
            (StatusCodes.Status404NotFound,
             "lockey_error_resource_not_found",
             LogLevel.Warning,
             null),

        HttpRequestException httpEx =>
            (StatusCodes.Status502BadGateway,
             "lockey_error_external_service_unavailable",
             LogLevel.Error,
             httpEx.StatusCode.HasValue
                ? new Dictionary<string, string> { ["statusCode"] = ((int)httpEx.StatusCode).ToString() }
                : null),

        _ =>
            (StatusCodes.Status500InternalServerError,
             "lockey_error_unexpected",
             LogLevel.Error,
             null)
    };
}
