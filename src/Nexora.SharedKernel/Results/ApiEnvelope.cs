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

    /// <summary>Creates a success envelope with data and optional localized message.</summary>
    public static ApiEnvelope<T> Success(T data, LocalizedMessage? message = null) => new()
    {
        Data = data,
        Message = message?.Key
    };

    /// <summary>Creates a failure envelope from an error with optional validation details.</summary>
    public static ApiEnvelope<T> Fail(Error error) => new()
    {
        Message = error.Message.Key,
        Meta = error.Message.Params.Count > 0 ? error.Message.Params : null,
        Errors = error.Details?.Select(d => new ApiValidationError(d.Message.Key, d.Message.Params)).ToList()
    };

    /// <summary>Creates a validation failure envelope with a list of field-level errors.</summary>
    public static ApiEnvelope<T> ValidationFail(List<ApiValidationError> errors) => new()
    {
        Message = "lockey_validation_failed",
        Errors = errors
    };
}

/// <summary>Represents a single validation error with a localization key and optional parameters.</summary>
public sealed record ApiValidationError(string Key, Dictionary<string, string>? Params = null);
