namespace Stratum.Api.Middleware;

/// <summary>
/// Middleware that enforces rate limiting on API requests.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, DateTime> _requestTimestamps = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.TraceIdentifier;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path;
        var method = context.Request.Method;

        var maxRequestsPerMinute = _configuration.GetValue<int>("RateLimit:MaxRequestsPerMinute", 100);

        if (maxRequestsPerMinute <= 0)
        {
            await _next(context);
            return;
        }

        var key = $"{clientIp}:{path}";

        await _semaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);

            // Clean up old entries
            var oldEntries = _requestTimestamps.Where(kvp => kvp.Value < oneMinuteAgo).ToList();
            foreach (var entry in oldEntries)
            {
                _requestTimestamps.Remove(entry.Key);
            }

            // Count requests from this client in the last minute
            var recentRequests = _requestTimestamps.Count(kvp => kvp.Key.StartsWith($"{clientIp}:") && kvp.Value > oneMinuteAgo);

            if (recentRequests >= maxRequestsPerMinute)
            {
                _logger.LogWarning(
                    "Rate limit exceeded: {RequestId} - Client {ClientIp} has made {RequestCount} requests in the last minute",
                    requestId, clientIp, recentRequests);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = "Rate Limit Exceeded",
                    Message = $"Maximum {maxRequestsPerMinute} requests per minute allowed",
                    RetryAfter = 60
                });
                return;
            }

            // Record this request
            _requestTimestamps[key] = now;
        }
        finally
        {
            _semaphore.Release();
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for RateLimitMiddleware.
/// </summary>
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitMiddleware>();
    }
}
