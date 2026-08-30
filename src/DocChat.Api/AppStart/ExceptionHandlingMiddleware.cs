using DocChat.Api.Exceptions;
using DocChat.Api.Models;

namespace DocChat.Api.AppStart;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(
                ex,
                "Request {Method} {Path} failed with status {StatusCode}: {Message}",
                context.Request.Method,
                context.Request.Path,
                ex.StatusCode,
                ex.Message);

            await WriteErrorAsync(context, ex.StatusCode, ex.Message, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {Method} {Path}", context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "Internal server error.", context.RequestAborted);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message, CancellationToken ct)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(message), ct);
    }
}
