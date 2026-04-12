namespace Stratum.Domain.Services;

/// <summary>
/// Provides validation for S3 object keys.
/// Object keys must follow specific rules to ensure compatibility with S3 clients.
/// </summary>
public sealed class ObjectKeyValidator
{
    private static readonly HashSet<char> InvalidKeyCharacters = new()
    {
        '\0', // Null character
        '\r', // Carriage return
        '\n'  // Line feed
    };

    private const int MinKeyLength = 1;
    private const int MaxKeyLength = 1024;

    /// <summary>
    /// Validates an object key.
    /// </summary>
    /// <param name="key">The object key to validate.</param>
    /// <returns>True if the key is valid; otherwise, false.</returns>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        // Length check
        if (key.Length < MinKeyLength || key.Length > MaxKeyLength)
        {
            return false;
        }

        // Character check
        if (key.Any(c => InvalidKeyCharacters.Contains(c)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates an object key and throws an exception if invalid.
    /// </summary>
    /// <param name="key">The object key to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        if (key.Length < MinKeyLength)
        {
            throw new ArgumentException($"Object key must be at least {MinKeyLength} character.", nameof(key));
        }

        if (key.Length > MaxKeyLength)
        {
            throw new ArgumentException($"Object key cannot exceed {MaxKeyLength} characters.", nameof(key));
        }

        if (key.Any(c => InvalidKeyCharacters.Contains(c)))
        {
            throw new ArgumentException("Object key contains invalid characters (null, carriage return, or line feed).", nameof(key));
        }
    }

    /// <summary>
    /// Normalizes an object key by ensuring it doesn't start or end with a slash.
    /// </summary>
    /// <param name="key">The object key to normalize.</param>
    /// <returns>The normalized object key.</returns>
    public static string Normalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        // Remove leading slashes
        var normalized = key.TrimStart('/');

        // Remove trailing slashes (but keep the root key if it's just a slash)
        if (normalized.Length > 1)
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    /// <summary>
    /// Extracts the directory prefix from an object key.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <returns>The directory prefix, or null if there is no directory.</returns>
    public static string? GetPrefix(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        var lastSlashIndex = key.LastIndexOf('/');
        return lastSlashIndex >= 0 ? key[..lastSlashIndex] : null;
    }

    /// <summary>
    /// Extracts the file name from an object key.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <returns>The file name.</returns>
    public static string GetName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        var lastSlashIndex = key.LastIndexOf('/');
        return lastSlashIndex >= 0 ? key[(lastSlashIndex + 1)..] : key;
    }

    /// <summary>
    /// Checks if a key is within a specific directory (prefix).
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="prefix">The directory prefix to check.</param>
    /// <returns>True if the key is within the directory; otherwise, false.</returns>
    public static bool IsInDirectory(string key, string prefix)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        // Ensure prefix ends with a slash for proper directory matching
        var normalizedPrefix = prefix.EndsWith('/') ? prefix : $"{prefix}/";
        return key.StartsWith(normalizedPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a key matches a given prefix.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="prefix">The prefix to check.</param>
    /// <returns>True if the key starts with the prefix; otherwise, false.</returns>
    public static bool MatchesPrefix(string key, string prefix)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(key));
        }

        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        return key.StartsWith(prefix, StringComparison.Ordinal);
    }
}
