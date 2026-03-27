using System.Diagnostics;
using System.Text.Json.Serialization;
using Nexora.SharedKernel.Localization;

namespace Nexora.SharedKernel.Results;

/// <summary>
/// Standard API response envelope. All API responses MUST use this format.
/// </summary>
public sealed record ApiEnvelope<T>
{
    public T? Data { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, string>? Meta { get; init; }
    public List<ApiValidationError>? Errors { get; init; }

    /// <summary>Trace ID for correlating responses with backend logs/traces.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    /// <summary>Creates a success envelope with data and optional localized message.</summary>
    public static ApiEnvelope<T> Success(T data, LocalizedMessage? message = null) => new()
    {
        Data = data,
        Message = message?.Key,
        TraceId = Activity.Current?.TraceId.ToString()
    };

    /// <summary>Creates a failure envelope from an error with optional validation details.</summary>
    public static ApiEnvelope<T> Fail(Error error, string? traceId = null) => new()
    {
        Message = error.Message.Key,
        Meta = error.Message.Params.Count > 0 ? error.Message.Params : null,
        Errors = error.Details?.Select(d => new ApiValidationError(d.Message.Key, d.Message.Params)).ToList(),
        TraceId = traceId ?? Activity.Current?.TraceId.ToString()
    };

    /// <summary>Creates a validation failure envelope with a list of field-level errors.</summary>
    public static ApiEnvelope<T> ValidationFail(List<ApiValidationError> errors, string? traceId = null) => new()
    {
        Message = "lockey_validation_failed",
        Errors = errors,
        TraceId = traceId
    };
}

/// <summary>
/// Non-generic helper for API responses that carry no data (e.g. delete/archive operations).
/// </summary>
public static class ApiEnvelope
{
    /// <summary>Creates a success envelope with no data payload — used for delete/archive-style operations.</summary>
    public static ApiEnvelope<object> Success(LocalizedMessage? message = null) =>
        ApiEnvelope<object>.Success(default!, message);

    /// <summary>Creates a failure envelope with no data payload — used for delete/archive-style operations.</summary>
    public static ApiEnvelope<object> Fail(Error error, string? traceId = null) =>
        ApiEnvelope<object>.Fail(error, traceId);
}

/// <summary>Represents a single validation error with a localization key and optional parameters.</summary>
public sealed record ApiValidationError(string Key, Dictionary<string, string>? Params = null);
