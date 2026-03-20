using Nexora.SharedKernel.Localization;

namespace Nexora.SharedKernel.Results;

/// <summary>
/// Represents the outcome of an operation without a return value.
/// Use factory methods to create success or failure results.
/// </summary>
public sealed class Result
{
    /// <summary>Indicates whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indicates whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Optional localized message associated with the result.</summary>
    public LocalizedMessage? Message { get; }

    /// <summary>Error details when the operation failed; null on success.</summary>
    public Error? Error { get; }

    private Result(bool isSuccess, LocalizedMessage? message, Error? error)
    {
        IsSuccess = isSuccess;
        Message = message;
        Error = error;
    }

    /// <summary>Creates a successful result with an optional message.</summary>
    public static Result Success(LocalizedMessage? message = null) =>
        new(true, message, null);

    /// <summary>Creates a failure result from an error.</summary>
    public static Result Failure(Error error) =>
        new(false, null, error);

    /// <summary>Creates a failure result from a localized message.</summary>
    public static Result Failure(LocalizedMessage message) =>
        new(false, null, new Error(message));

    /// <summary>Creates a failure result from a localization key and optional parameters.</summary>
    public static Result Failure(string localizationKey, Dictionary<string, string>? @params = null) =>
        new(false, null, new Error(new LocalizedMessage(localizationKey, @params)));

    /// <summary>Creates a successful typed result with a value and optional message.</summary>
    public static Result<T> Success<T>(T value, LocalizedMessage? message = null) =>
        Result<T>.Success(value, message);

    /// <summary>Creates a typed failure result from an error.</summary>
    public static Result<T> Failure<T>(Error error) =>
        Result<T>.Failure(error);

    /// <summary>Creates a typed failure result from a localization key and optional parameters.</summary>
    public static Result<T> Failure<T>(string localizationKey, Dictionary<string, string>? @params = null) =>
        Result<T>.Failure(new Error(new LocalizedMessage(localizationKey, @params)));
}

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="T"/>.
/// Use factory methods to create success or failure results.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public sealed class Result<T>
{
    /// <summary>Indicates whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Indicates whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The value returned on success; default on failure.</summary>
    public T? Value { get; }

    /// <summary>Optional localized message associated with the result.</summary>
    public LocalizedMessage? Message { get; }

    /// <summary>Error details when the operation failed; null on success.</summary>
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, LocalizedMessage? message, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
        Error = error;
    }

    /// <summary>Creates a successful result with the given value and optional message.</summary>
    public static Result<T> Success(T value, LocalizedMessage? message = null) =>
        new(true, value, message, null);

    /// <summary>Creates a failure result from an error.</summary>
    public static Result<T> Failure(Error error) =>
        new(false, default, null, error);

    /// <summary>Creates a failure result from a localized message.</summary>
    public static Result<T> Failure(LocalizedMessage message) =>
        new(false, default, null, new Error(message));

    /// <summary>Creates a failure result from a localization key and optional parameters.</summary>
    public static Result<T> Failure(string localizationKey, Dictionary<string, string>? @params = null) =>
        new(false, default, null, new Error(new LocalizedMessage(localizationKey, @params)));
}

/// <summary>
/// Represents an error with a localized message and optional child error details.
/// </summary>
/// <param name="Message">The localized error message.</param>
/// <param name="Details">Optional list of detailed sub-errors (e.g., validation errors).</param>
public sealed record Error(LocalizedMessage Message, List<Error>? Details = null);
