namespace UrlShortener.Core.Models;

public enum UrlErrorCode
{
    None = 0,
    InvalidUrl,
    NotFound,
    Disabled,
    CodeGenerationFailed
}

/// <summary>Lightweight result type so the service layer returns typed errors instead of throwing.</summary>
public readonly struct Result<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public UrlErrorCode Error { get; }
    public string? Message { get; }

    private Result(bool success, T? value, UrlErrorCode error, string? message)
    {
        Success = success;
        Value = value;
        Error = error;
        Message = message;
    }

    public static Result<T> Ok(T value) => new(true, value, UrlErrorCode.None, null);
    public static Result<T> Fail(UrlErrorCode error, string message) => new(false, default, error, message);
}
