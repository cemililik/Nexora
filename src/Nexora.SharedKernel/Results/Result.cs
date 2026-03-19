using Nexora.SharedKernel.Localization;

namespace Nexora.SharedKernel.Results;

public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public LocalizedMessage? Message { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, LocalizedMessage? message, Error? error)
    {
        IsSuccess = isSuccess;
        Message = message;
        Error = error;
    }

    public static Result Success(LocalizedMessage? message = null) =>
        new(true, message, null);

    public static Result Failure(Error error) =>
        new(false, null, error);

    public static Result Failure(LocalizedMessage message) =>
        new(false, null, new Error(message));

    public static Result Failure(string localizationKey, Dictionary<string, string>? @params = null) =>
        new(false, null, new Error(new LocalizedMessage(localizationKey, @params)));

    public static Result<T> Success<T>(T value, LocalizedMessage? message = null) =>
        Result<T>.Success(value, message);

    public static Result<T> Failure<T>(Error error) =>
        Result<T>.Failure(error);

    public static Result<T> Failure<T>(string localizationKey, Dictionary<string, string>? @params = null) =>
        Result<T>.Failure(new Error(new LocalizedMessage(localizationKey, @params)));
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public LocalizedMessage? Message { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, LocalizedMessage? message, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Message = message;
        Error = error;
    }

    public static Result<T> Success(T value, LocalizedMessage? message = null) =>
        new(true, value, message, null);

    public static Result<T> Failure(Error error) =>
        new(false, default, null, error);

    public static Result<T> Failure(LocalizedMessage message) =>
        new(false, default, null, new Error(message));

    public static Result<T> Failure(string localizationKey, Dictionary<string, string>? @params = null) =>
        new(false, default, null, new Error(new LocalizedMessage(localizationKey, @params)));
}

public sealed record Error(LocalizedMessage Message, List<Error>? Details = null);
