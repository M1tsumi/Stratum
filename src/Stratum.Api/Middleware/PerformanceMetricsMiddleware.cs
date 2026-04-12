namespace Stratum.Api.Middleware;

using Stratum.Api.Metrics;

/// <summary>
/// Middleware that collects performance metrics for all API requests.
/// Records request duration and success/failure status.
/// </summary>
public sealed class PerformanceMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly PerformanceMetrics _metrics;

    public PerformanceMetricsMiddleware(RequestDelegate next, PerformanceMetrics metrics)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var operation = $"{context.Request.Method} {context.Request.Path}";
        var isError = false;

        try
        {
            await _next(context);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;

            if (isError || context.Response.StatusCode >= 400)
            {
                _metrics.RecordError(operation, durationMs);
            }
            else
            {
                _metrics.RecordSuccess(operation, durationMs);
            }
        }
    }
}

/// <summary>
/// Extension methods for registering PerformanceMetricsMiddleware.
/// </summary>
public static class PerformanceMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceMetrics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PerformanceMetricsMiddleware>();
    }
}
