using app.Constants;

namespace app.Application.Services;

public sealed class ServiceResult
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public object? Data { get; init; }
    public string? Message { get; init; }
    public ErrorDefinition? Error { get; init; }

    public static ServiceResult Success(object? data, string message, int statusCode = StatusCodes.Status200OK) =>
        new()
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Data = data,
            Message = message
        };

    public static ServiceResult Failure(ErrorDefinition error, int statusCode) =>
        new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Error = error
        };
}
