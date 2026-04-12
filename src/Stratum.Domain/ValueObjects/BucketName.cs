namespace Stratum.Domain.ValueObjects;

/// <summary>
/// Represents an S3 bucket name.
/// Bucket names must be DNS-compliant and follow specific naming rules:
/// - Must be between 3 and 63 characters
/// - Must contain only lowercase letters, numbers, and hyphens
/// - Must start and end with a letter or number
/// - Must not be formatted as an IP address (e.g., 192.168.1.1)
/// - Must not contain underscores
/// </summary>
public sealed record BucketName
{
    /// <summary>
    /// Gets the bucket name value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the BucketName class.
    /// </summary>
    /// <param name="value">The bucket name value.</param>
    public BucketName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Bucket name cannot be null or whitespace.", nameof(value));
        }

        if (!IsValid(value))
        {
            throw new ArgumentException($"Invalid bucket name: {value}. Bucket names must be DNS-compliant: 3-63 characters, lowercase letters, numbers, and hyphens only, must start and end with letter or number.", nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    /// <summary>
    /// Determines whether the specified name is a valid S3 bucket name.
    /// </summary>
    /// <param name="name">The bucket name to validate.</param>
    /// <returns>True if the name is valid; otherwise, false.</returns>
    public static bool IsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Length check
        if (name.Length < 3 || name.Length > 63)
        {
            return false;
        }

        // Character set check (lowercase letters, numbers, hyphens only)
        if (!name.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-'))
        {
            return false;
        }

        // Must start and end with letter or number
        if (!char.IsLetterOrDigit(name[0]) || !char.IsLetterOrDigit(name[^1]))
        {
            return false;
        }

        // Must not contain consecutive hyphens
        if (name.Contains("--"))
        {
            return false;
        }

        // Must not be formatted as an IP address
        if (System.Net.IPAddress.TryParse(name, out _))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a BucketName from the specified string.
    /// </summary>
    /// <param name="name">The bucket name.</param>
    /// <returns>A new BucketName instance if valid; otherwise, null.</returns>
    public static BucketName? TryCreate(string name)
    {
        if (IsValid(name))
        {
            return new BucketName(name);
        }
        return null;
    }

    /// <summary>
    /// Implicitly converts a BucketName to its string value.
    /// </summary>
    /// <param name="bucketName">The BucketName to convert.</param>
    /// <returns>The bucket name value as a string.</returns>
    public static implicit operator string(BucketName bucketName) => bucketName.Value;

    /// <summary>
    /// Returns the string representation of the bucket name.
    /// </summary>
    /// <returns>The bucket name value.</returns>
    public override string ToString() => Value;
}
