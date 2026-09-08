using System.Net;
using System.Text.Json;
using BistQuant.Application.Common.Exceptions;

namespace BistQuant.API.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        var code = "INTERNAL_SERVER_ERROR";
        var message = "An unexpected error occurred.";
        var details = new List<string>();

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = HttpStatusCode.BadRequest;
                code = "VALIDATION_ERROR";
                message = validationEx.Message;
                details = validationEx.Errors.SelectMany(kv => kv.Value.Select(err => $"{kv.Key}: {err}")).ToList();
                break;

            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                code = "NOT_FOUND";
                message = notFoundEx.Message;
                break;

            case BusinessException bizEx:
                statusCode = HttpStatusCode.BadRequest;
                code = bizEx.ErrorCode;
                message = bizEx.Message;
                break;

            case AppException appEx:
                statusCode = HttpStatusCode.BadRequest;
                code = appEx.ErrorCode;
                message = appEx.Message;
                break;

            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                code = "BAD_REQUEST";
                message = argEx.Message;
                break;

            case InvalidOperationException invOpEx:
                statusCode = HttpStatusCode.BadRequest;
                code = "INVALID_OPERATION";
                message = invOpEx.Message;
                break;

            case UnauthorizedAccessException unauthEx:
                statusCode = HttpStatusCode.Unauthorized;
                code = "UNAUTHORIZED";
                message = unauthEx.Message;
                break;

            case KeyNotFoundException knfEx:
                statusCode = HttpStatusCode.NotFound;
                code = "NOT_FOUND";
                message = knfEx.Message;
                break;

            default:
                message = exception.Message;
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            code,
            message,
            details
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
