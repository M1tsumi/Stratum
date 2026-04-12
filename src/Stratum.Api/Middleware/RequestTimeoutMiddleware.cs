namespace Stratum.Api.Middleware;

/// <summary>
/// Middleware that enforces request timeout limits to prevent long-running requests.
/// </summary>
public class RequestTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimeoutMiddleware> _logger;
    private readonly TimeSpan _timeout;

    public RequestTimeoutMiddleware(RequestDelegate next, ILogger<RequestTimeoutMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        var timeoutSeconds = configuration.GetValue<int>("Server:RequestTimeoutSeconds", 300); // 5 minutes default
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var path = context.Request.Path;
        var method = context.Request.Method;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        cts.CancelAfter(_timeout);

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Request timeout: {RequestId} - {Method} {Path} exceeded timeout of {Timeout}",
                requestId, method, path, _timeout);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = "Request Timeout",
                    Message = $"Request exceeded timeout of {_timeout.TotalMinutes} minutes",
                    RequestId = requestId
                }, context.RequestAborted);
            }
        }
    }
}

/// <summary>
/// Extension methods for RequestTimeoutMiddleware.
/// </summary>
public static class RequestTimeoutMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTimeout(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestTimeoutMiddleware>();
    }
}
