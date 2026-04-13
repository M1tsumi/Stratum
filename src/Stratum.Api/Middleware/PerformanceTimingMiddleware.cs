namespace Stratum.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Middleware that measures and logs request performance metrics.
/// </summary>
public class PerformanceTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceTimingMiddleware> _logger;

    public PerformanceTimingMiddleware(RequestDelegate next, ILogger<PerformanceTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var path = context.Request.Path;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            // Log slow requests (> 1000ms)
            if (elapsed > 1000)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestId} {Path} - {Elapsed}ms",
                    requestId, path, elapsed);
            }
            else if (elapsed > 500)
            {
                _logger.LogInformation(
                    "Request performance: {RequestId} {Path} - {Elapsed}ms",
                    requestId, path, elapsed);
            }

            // Add performance header
            context.Response.Headers.Append("X-Response-Time", $"{elapsed}ms");
        }
    }
}

/// <summary>
/// Extension methods for PerformanceTimingMiddleware.
/// </summary>
public static class PerformanceTimingMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceTiming(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceTimingMiddleware>();
    }
}
