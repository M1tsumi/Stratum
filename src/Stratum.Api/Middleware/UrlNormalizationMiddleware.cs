namespace Stratum.Api.Middleware;

/// <summary>
/// Middleware for normalizing S3 URLs to extract bucket and key information.
/// Supports both virtual-hosted style (bucket.domain.com/key) and path style (domain.com/bucket/key).
/// </summary>
public sealed class UrlNormalizationMiddleware
{
    private const string BucketKey = "S3Bucket";
    private const string ObjectKey = "S3Key";
    private const string UrlStyleKey = "S3UrlStyle";

    private readonly RequestDelegate _next;
    private readonly string _hostPattern;

    /// <summary>
    /// Initializes a new instance of the UrlNormalizationMiddleware class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="hostPattern">The host pattern for virtual-hosted style URLs (e.g., *.s3.example.com).</param>
    public UrlNormalizationMiddleware(RequestDelegate next, string hostPattern = "*.s3.localhost")
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _hostPattern = hostPattern;
    }

    /// <summary>
    /// Processes the HTTP request to normalize the URL and extract bucket/key information.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var path = context.Request.Path.Value ?? string.Empty;

        // Check for virtual-hosted style URL
        if (IsVirtualHostedStyle(host, out var bucketName))
        {
            context.Items[BucketKey] = bucketName;
            context.Items[ObjectKey] = path.TrimStart('/');
            context.Items[UrlStyleKey] = "virtual-hosted";
        }
        // Check for path style URL
        else if (IsPathStyle(path, out bucketName))
        {
            var key = ExtractKeyFromPath(path);
            context.Items[BucketKey] = bucketName;
            context.Items[ObjectKey] = key;
            context.Items[UrlStyleKey] = "path";
        }
        else
        {
            // Service-level operation (e.g., ListBuckets)
            context.Items[BucketKey] = null;
            context.Items[ObjectKey] = null;
            context.Items[UrlStyleKey] = "service";
        }

        await _next(context);
    }

    /// <summary>
    /// Determines if the host is in virtual-hosted style and extracts the bucket name.
    /// </summary>
    private bool IsVirtualHostedStyle(string host, out string? bucketName)
    {
        if (string.IsNullOrEmpty(host))
        {
            bucketName = null;
            return false;
        }

        // Check if host matches the pattern (e.g., bucket.s3.localhost)
        var parts = host.Split('.');
        
        // For virtual-hosted style, we expect at least 3 parts: bucket.s3.domain
        if (parts.Length >= 3)
        {
            bucketName = parts[0];
            return IsValidBucketName(bucketName);
        }

        bucketName = null;
        return false;
    }

    /// <summary>
    /// Determines if the path is in path style and extracts the bucket name.
    /// </summary>
    private bool IsPathStyle(string path, out string? bucketName)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            bucketName = null;
            return false;
        }

        // Remove leading slash and split
        var parts = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length > 0)
        {
            bucketName = parts[0];
            return IsValidBucketName(bucketName);
        }

        bucketName = null;
        return false;
    }

    /// <summary>
    /// Extracts the object key from a path-style URL.
    /// </summary>
    private string ExtractKeyFromPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return string.Empty;
        }

        var parts = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length > 1)
        {
            return string.Join('/', parts.Skip(1));
        }

        return string.Empty;
    }

    /// <summary>
    /// Validates if a string is a valid S3 bucket name.
    /// </summary>
    private bool IsValidBucketName(string bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return false;
        }

        // Length check
        if (bucketName.Length < 3 || bucketName.Length > 63)
        {
            return false;
        }

        // Character set check (lowercase letters, numbers, hyphens only)
        if (!bucketName.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-'))
        {
            return false;
        }

        // Must start and end with letter or number
        if (!char.IsLetterOrDigit(bucketName[0]) || !char.IsLetterOrDigit(bucketName[^1]))
        {
            return false;
        }

        // Must not contain consecutive hyphens
        if (bucketName.Contains("--"))
        {
            return false;
        }

        // Must not be formatted as an IP address
        if (System.Net.IPAddress.TryParse(bucketName, out _))
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Extension methods for registering URL normalization middleware.
/// </summary>
public static class UrlNormalizationMiddlewareExtensions
{
    /// <summary>
    /// Adds the URL normalization middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="hostPattern">The host pattern for virtual-hosted style URLs.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseUrlNormalization(
        this IApplicationBuilder builder,
        string hostPattern = "*.s3.localhost")
    {
        return builder.UseMiddleware<UrlNormalizationMiddleware>(hostPattern);
    }
}
