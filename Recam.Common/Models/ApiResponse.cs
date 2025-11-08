namespace Recam.Common.Models;

public class ApiResponse<T>
{
    public bool Succeed { get; init; }
    public T Data { get; init; }
    public string? Message { get; init; }
    public ApiError? Error { get; init; }
    public object? Meta  { get; init; }    // 分頁信息可能


    public static ApiResponse<T> Success(T data, string? message = null, object? meta = null)
    {
        return new ApiResponse<T>
        {
            Succeed = true,
            Data = data,
            Message = message,
            Meta = meta,
        };
    }

    public static ApiResponse<T> Fail(string message, string code = "Bad Request", object? details = null)
    {
        return new ApiResponse<T>
        {
            Succeed = false,
            Error = new ApiError{Code = code, Message = message, Details = details}
        };
    }
}

public class ApiError
{
    public string Code { get; set; } = "ERROR";
    public string Message { get; set; } = "An error occurred";
    public object? Details { get; set; }
    public string? TraceId { get; set; }
}