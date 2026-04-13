namespace Stratum.Api.Middleware;

using Microsoft.AspNetCore.Http;
using System.Net;

/// <summary>
/// Middleware for validating HTTP requests before they reach the endpoints.
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate request headers
        if (!await ValidateHeadersAsync(context))
        {
            return;
        }

        // Validate request content length
        if (!await ValidateContentLengthAsync(context))
        {
            return;
        }

        await _next(context);
    }

    private async Task<bool> ValidateHeadersAsync(HttpContext context)
    {
        // Check for required headers for S3 API
        var path = context.Request.Path.Value ?? string.Empty;

        // For object operations, require Content-Length for PUT requests
        if (context.Request.Method == "PUT" && path.Count(c => c == '/') >= 2)
        {
            if (!context.Request.Headers.ContainsKey("Content-Length"))
            {
                _logger.LogWarning("Missing Content-Length header for PUT request to {Path}", path);
                context.Response.StatusCode = (int)HttpStatusCode.LengthRequired;
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = new
                    {
                        Code = "MissingContentLength",
                        Message = "Content-Length header is required for PUT requests",
                        Resource = path
                    }
                });
                return false;
            }
        }

        // Validate Content-Type for POST/PUT requests
        if ((context.Request.Method == "POST" || context.Request.Method == "PUT") &&
            context.Request.ContentLength > 0)
        {
            var contentType = context.Request.ContentType;
            if (string.IsNullOrEmpty(contentType))
            {
                _logger.LogWarning("Missing Content-Type header for {Method} request to {Path}", context.Request.Method, path);
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = new
                    {
                        Code = "MissingContentType",
                        Message = "Content-Type header is required for requests with a body",
                        Resource = path
                    }
                });
                return false;
            }
        }

        return true;
    }

    private async Task<bool> ValidateContentLengthAsync(HttpContext context)
    {
        // Validate that Content-Length header matches actual content length
        if (context.Request.Headers.ContainsKey("Content-Length") && 
            long.TryParse(context.Request.Headers["Content-Length"], out var contentLength))
        {
            // Check for unreasonably large content length (5GB limit)
            const long maxContentLength = 5L * 1024 * 1024 * 1024; // 5GB
            if (contentLength > maxContentLength)
            {
                _logger.LogWarning("Content-Length {ContentLength} exceeds maximum allowed {MaxContentLength}", contentLength, maxContentLength);
                context.Response.StatusCode = 413; // Payload Too Large
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = new
                    {
                        Code = "EntityTooLarge",
                        Message = $"Your proposed upload exceeds the maximum allowed size of {maxContentLength / (1024 * 1024)} MB",
                        Resource = context.Request.Path.Value
                    }
                });
                return false;
            }

            // Check for negative content length
            if (contentLength < 0)
            {
                _logger.LogWarning("Invalid negative Content-Length: {ContentLength}", contentLength);
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = new
                    {
                        Code = "InvalidContentLength",
                        Message = "Content-Length cannot be negative",
                        Resource = context.Request.Path.Value
                    }
                });
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Extension methods for registering the request validation middleware.
/// </summary>
public static class RequestValidationMiddlewareExtensions
{
    /// <summary>
    /// Adds request validation middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRequestValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestValidationMiddleware>();
    }
}
