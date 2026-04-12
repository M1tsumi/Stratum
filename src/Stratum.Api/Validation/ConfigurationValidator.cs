namespace Stratum.Api.Validation;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Validates application configuration at startup.
/// Ensures all required settings are present and valid.
/// </summary>
public sealed class ConfigurationValidator
{
    private readonly IConfiguration _configuration;

    public ConfigurationValidator(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Validates all configuration settings.
    /// </summary>
    /// <returns>A list of validation errors, or empty if validation passes.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        // Validate storage configuration
        ValidateStorageConfiguration(errors);

        // Validate server configuration
        ValidateServerConfiguration(errors);

        // Validate CORS configuration
        ValidateCorsConfiguration(errors);

        return errors;
    }

    private void ValidateStorageConfiguration(List<string> errors)
    {
        var dataDirectory = _configuration["Storage:DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            errors.Add("Storage:DataDirectory is required. Set it to a valid directory path.");
        }
        else
        {
            try
            {
                // Check if directory exists or can be created
                var dirInfo = new DirectoryInfo(dataDirectory);
                if (!dirInfo.Exists)
                {
                    dirInfo.Create();
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Storage:DataDirectory '{dataDirectory}' is invalid or not accessible: {ex.Message}");
            }
        }

        // Validate concurrent operations limit
        var maxConcurrentOps = _configuration.GetValue<int>("Storage:MaxConcurrentOperations", 100);
        if (maxConcurrentOps <= 0)
        {
            errors.Add("Storage:MaxConcurrentOperations must be greater than 0.");
        }
        if (maxConcurrentOps > 1000)
        {
            errors.Add($"Storage:MaxConcurrentOperations is set to {maxConcurrentOps}, which may cause system overload. Recommended maximum is 100.");
        }

        // Validate cache configuration
        var maxCacheSize = _configuration.GetValue<int>("Storage:MaxCacheSize", 1000);
        if (maxCacheSize <= 0)
        {
            errors.Add("Storage:MaxCacheSize must be greater than 0.");
        }

        var maxCacheBytes = _configuration.GetValue<long>("Storage:MaxCacheBytes", 1024L * 1024 * 1024);
        if (maxCacheBytes <= 0)
        {
            errors.Add("Storage:MaxCacheBytes must be greater than 0.");
        }
        if (maxCacheBytes > 10L * 1024 * 1024 * 1024) // 10GB
        {
            errors.Add($"Storage:MaxCacheBytes is set to {maxCacheBytes / (1024 * 1024 * 1024)}GB, which may cause memory pressure. Recommended maximum is 1GB.");
        }
    }

    private void ValidateServerConfiguration(List<string> errors)
    {
        var listenUrl = _configuration["Server:ListenUrl"];
        if (string.IsNullOrWhiteSpace(listenUrl))
        {
            errors.Add("Server:ListenUrl is required. Set it to a valid URL (e.g., http://0.0.0.0:9000).");
        }
        else
        {
            try
            {
                var uri = new Uri(listenUrl);
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    errors.Add($"Server:ListenUrl must use http or https scheme. Current scheme: {uri.Scheme}");
                }
            }
            catch (UriFormatException ex)
            {
                errors.Add($"Server:ListenUrl '{listenUrl}' is not a valid URL: {ex.Message}");
            }
        }

        // Validate request body size limit
        var maxRequestBodySize = _configuration.GetValue<long>("Server:MaxRequestBodySize", 5L * 1024 * 1024 * 1024);
        if (maxRequestBodySize <= 0)
        {
            errors.Add("Server:MaxRequestBodySize must be greater than 0.");
        }
        if (maxRequestBodySize > 100L * 1024 * 1024 * 1024) // 100GB
        {
            errors.Add($"Server:MaxRequestBodySize is set to {maxRequestBodySize / (1024 * 1024 * 1024)}GB, which may cause memory issues. Recommended maximum is 5GB.");
        }

        // Validate request buffer size
        var maxRequestBufferSize = _configuration.GetValue<int>("Server:MaxRequestBufferSize", 1024 * 1024);
        if (maxRequestBufferSize <= 0)
        {
            errors.Add("Server:MaxRequestBufferSize must be greater than 0.");
        }
        if (maxRequestBufferSize > 10 * 1024 * 1024) // 10MB
        {
            errors.Add($"Server:MaxRequestBufferSize is set to {maxRequestBufferSize / (1024 * 1024)}MB, which may cause memory issues. Recommended maximum is 1MB.");
        }
    }

    private void ValidateCorsConfiguration(List<string> errors)
    {
        var allowedOrigins = _configuration["Cors:AllowedOrigins"];
        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            errors.Add("Cors:AllowedOrigins is required. Set it to '*' or a comma-separated list of allowed origins.");
        }
        else if (allowedOrigins != "*")
        {
            try
            {
                var origins = allowedOrigins.Split(',');
                foreach (var origin in origins)
                {
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        var uri = new Uri(origin.Trim());
                    }
                }
            }
            catch (UriFormatException ex)
            {
                errors.Add($"Cors:AllowedOrigins contains invalid URLs: {ex.Message}");
            }
        }
    }
}
