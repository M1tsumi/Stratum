namespace Stratum.Api.Middleware;

using Serilog;

/// <summary>
/// Middleware that logs HTTP requests and responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.Value;

        // Log request
        _logger.LogInformation(
            "Request: {RequestId} {Method} {Path}{QueryString}",
            requestId, method, path, queryString);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            var statusCode = context.Response.StatusCode;
            var duration = stopwatch.ElapsedMilliseconds;

            var logLevel = statusCode >= 500 ? LogLevel.Error 
                          : statusCode >= 400 ? LogLevel.Warning 
                          : LogLevel.Information;

            _logger.Log(logLevel,
                "Response: {RequestId} {Method} {Path} - {StatusCode} - {Duration}ms",
                requestId, method, path, statusCode, duration);
        }
    }
}

/// <summary>
/// Extension methods for RequestLoggingMiddleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
