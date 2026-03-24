using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Nexora.SharedKernel.Results;

namespace Nexora.Host;

/// <summary>
/// Endpoint filter that injects TraceId into ApiEnvelope error responses (4xx/5xx).
/// This ensures endpoint-level errors include the same TraceId as GlobalExceptionHandler errors.
/// </summary>
public sealed class TraceIdEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var result = await next(context);

        if (result is IResult && result is IStatusCodeHttpResult statusCodeResult
            && statusCodeResult.StatusCode is >= 400)
        {
            var traceId = Activity.Current?.TraceId.ToString()
                ?? context.HttpContext.TraceIdentifier;

            if (result is IValueHttpResult valueResult && valueResult.Value is { } envelope)
            {
                var envelopeType = envelope.GetType();
                if (envelopeType.IsGenericType
                    && envelopeType.GetGenericTypeDefinition() == typeof(ApiEnvelope<>))
                {
                    var traceIdProp = envelopeType.GetProperty(nameof(ApiEnvelope<object>.TraceId));
                    if (traceIdProp is not null && traceIdProp.GetValue(envelope) is null)
                    {
                        // Records use 'with' — reconstruct via reflection on the <T> type
                        var cloneMethod = envelopeType.GetMethod("<Clone>$")
                            ?? throw new InvalidOperationException(
                                $"ApiEnvelope<> type {envelopeType.Name} is missing <Clone>$ method");
                        var withTraceId = cloneMethod.Invoke(envelope, null)
                            ?? throw new InvalidOperationException(
                                $"Clone of {envelopeType.Name} returned null");
                        traceIdProp.SetValue(withTraceId, traceId);

                        var statusCode = statusCodeResult.StatusCode!.Value;
                        return Results.Json(withTraceId, statusCode: statusCode,
                            contentType: "application/json");
                    }
                }
            }
        }

        return result;
    }
}
