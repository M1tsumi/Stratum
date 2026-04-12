namespace Stratum.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Middleware that adds a unique request ID to each request for tracing.
/// </summary>
public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or create request ID
        var requestId = context.TraceIdentifier;
        
        // Add to response headers for debugging
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-Request-Id", requestId);
            return Task.CompletedTask;
        });

        // Add to activity for distributed tracing
        Activity.Current?.AddTag("request.id", requestId);

        await _next(context);
    }
}

/// <summary>
/// Extension methods for RequestIdMiddleware.
/// </summary>
public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestIdMiddleware>();
    }
}
