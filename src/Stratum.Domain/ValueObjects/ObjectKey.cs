namespace Stratum.Domain.ValueObjects;

/// <summary>
/// S3 object key (supports / for directories, 1-1024 chars).
/// </summary>
public sealed record ObjectKey
{
    private static readonly HashSet<char> InvalidKeyCharacters = new()
    {
        '\0', '\r', '\n'
    };

    /// <summary>
    /// Key value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Prefix (before last /).
    /// </summary>
    public string? Prefix
    {
        get
        {
            var lastSlashIndex = Value.LastIndexOf('/');
            return lastSlashIndex >= 0 ? Value[..lastSlashIndex] : null;
        }
    }

    /// <summary>
    /// Name (after last /).
    /// </summary>
    public string Name
    {
        get
        {
            var lastSlashIndex = Value.LastIndexOf('/');
            return lastSlashIndex >= 0 ? Value[(lastSlashIndex + 1)..] : Value;
        }
    }

    /// <summary>
    /// Creates object key.
    /// </summary>
    public ObjectKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Object key cannot be null or whitespace.", nameof(value));
        }

        if (value.Length == 0 || value.Length > 1024)
        {
            throw new ArgumentException("Object key must be between 1 and 1024 characters.", nameof(value));
        }

        if (value.Any(c => InvalidKeyCharacters.Contains(c)))
        {
            throw new ArgumentException("Object key contains invalid characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Validates key format.
    /// </summary>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (key.Length == 0 || key.Length > 1024)
        {
            return false;
        }

        if (key.Any(c => InvalidKeyCharacters.Contains(c)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if key starts with prefix.
    /// </summary>
    public bool StartsWith(string prefix)
    {
        return Value.StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if key is in directory.
    /// </summary>
    public bool IsInDirectory(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        // Ensure prefix ends with a slash for proper directory matching
        var normalizedPrefix = prefix.EndsWith('/') ? prefix : $"{prefix}/";
        return Value.StartsWith(normalizedPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicit string conversion.
    /// </summary>
    public static implicit operator string(ObjectKey key) => key.Value;

    /// <summary>
    /// String representation.
    /// </summary>
    public override string ToString() => Value;
}
