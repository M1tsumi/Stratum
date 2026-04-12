namespace Stratum.Api.Middleware;

using Microsoft.AspNetCore.Http.Features;

/// <summary>
/// Middleware that validates request size limits before processing.
/// </summary>
public class RequestSizeValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestSizeValidationMiddleware> _logger;
    private readonly long _maxRequestSizeBytes;

    public RequestSizeValidationMiddleware(RequestDelegate next, ILogger<RequestSizeValidationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxRequestSizeBytes = configuration.GetValue<long>("Server:MaxRequestBodySize", 5L * 1024 * 1024 * 1024); // 5GB default
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;

        // Check Content-Length header if present
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > _maxRequestSizeBytes)
        {
            _logger.LogWarning(
                "Request rejected: {RequestId} - Content-Length {ContentLength} exceeds maximum {MaxSize}",
                requestId, context.Request.ContentLength.Value, _maxRequestSizeBytes);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new
            {
                Error = "Request too large",
                Message = $"Request body exceeds maximum size of {_maxRequestSizeBytes / (1024 * 1024)}MB",
                RequestId = requestId
            });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for RequestSizeValidationMiddleware.
/// </summary>
public static class RequestSizeValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestSizeValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestSizeValidationMiddleware>();
    }
}
