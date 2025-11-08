using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Recam.Common.Models;

namespace Recam.Common.Exceptions.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response has already started, rethrowing exception.");
                throw;
            }
            await WriteErrorAsync(context, ex);
        }
    }
    
    private async Task WriteErrorAsync(HttpContext httpContext, Exception ex)
    {
        var status = StatusCodes.Status500InternalServerError;
        var code   = "INTERNAL_ERROR";
        var msg    = "An error occurred while processing your request.";
        object? details = null;

        switch (ex)
        {
            case ValidationException ve:
                status = StatusCodes.Status400BadRequest;
                code = ve.code;
                msg = ve.Message;
                details = ve.Details;
                break;
            case BadRequestException:
                status = StatusCodes.Status400BadRequest;
                code   = "BAD_REQUEST";
                msg    = ex.Message;
                break;

            case UnauthorizedException:
                status = StatusCodes.Status401Unauthorized;
                code   = "UNAUTHORIZED";
                msg    = ex.Message;
                break;

            case ForbiddenException:
                status = StatusCodes.Status403Forbidden;
                code   = "FORBIDDEN";
                msg    = ex.Message;
                break;

            case NotFoundException:
                status = StatusCodes.Status404NotFound;
                code   = "NOT_FOUND";
                msg    = ex.Message;
                break;

            case ConflictException:
                status = StatusCodes.Status409Conflict;
                code   = "CONFLICT";
                msg    = ex.Message;
                break;

            case KeyNotFoundException:
                status = StatusCodes.Status404NotFound;
                code   = "NOT_FOUND";
                msg    = ex.Message;
                break;

            case OperationCanceledException:
            // 用戶中斷或逾時；可視需要改為 400/408
                status = 499; // 客戶端關閉請求
                code   = "CLIENT_CLOSED_REQUEST";
                msg    = "The request was canceled.";
                break;
        
            // others 500 ^_^
        }

        if (status >= 500)
        {
            _logger.LogError(ex, "Unhandled exception");
        }
        else
        {
            _logger.LogWarning(ex, "Handled domain/known exception");
        }
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode  = status;

        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var payload = new ApiResponse<object>
        {
            Succeed = false,
            Error = new ApiError
            {
                Code = code,
                Message = msg,
                Details = isDev ? (details ?? ex.ToString()) : null,   // DEV 顯示堆疊
                TraceId = httpContext.TraceIdentifier
            }
        
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await httpContext.Response.WriteAsync(json);
    }
}

