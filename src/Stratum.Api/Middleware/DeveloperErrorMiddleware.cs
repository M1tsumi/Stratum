namespace Stratum.Api.Middleware;

/// <summary>
/// Middleware that provides detailed error information in development mode.
/// </summary>
public class DeveloperErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public DeveloperErrorMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (_environment.IsDevelopment())
            {
                await HandleDevelopmentError(context, ex);
            }
            else
            {
                await HandleProductionError(context, ex);
            }
        }
    }

    private static Task HandleDevelopmentError(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var error = new
        {
            Type = ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException?.Message,
            RequestId = context.TraceIdentifier,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        return context.Response.WriteAsJsonAsync(error);
    }

    private static Task HandleProductionError(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var error = new
        {
            Message = "An internal error occurred. Please try again later.",
            RequestId = context.TraceIdentifier
        };

        return context.Response.WriteAsJsonAsync(error);
    }
}

/// <summary>
/// Extension methods for DeveloperErrorMiddleware.
/// </summary>
public static class DeveloperErrorMiddlewareExtensions
{
    public static IApplicationBuilder UseDeveloperError(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DeveloperErrorMiddleware>();
    }
}
