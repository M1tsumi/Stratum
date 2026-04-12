namespace Stratum.Domain.ValueObjects;

/// <summary>
/// Represents an entity tag (ETag) for S3 objects.
/// ETags are used for object versioning and change detection.
/// For single-part uploads, this is the MD5 hash of the content.
/// For multipart uploads, this is the MD5 hash of all part ETags with the part count suffix.
/// </summary>
public sealed record ETag
{
    /// <summary>
    /// Gets the ETag value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets whether this is a multipart upload ETag (contains part count suffix).
    /// </summary>
    public bool IsMultipart { get; }

    /// <summary>
    /// Gets the part count if this is a multipart ETag; otherwise, null.
    /// </summary>
    public int? PartCount { get; }

    /// <summary>
    /// Initializes a new instance of the ETag class.
    /// </summary>
    /// <param name="value">The ETag value.</param>
    public ETag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ETag value cannot be null or whitespace.", nameof(value));
        }

        Value = value;
        
        // Parse multipart ETag format: "hash-partCount"
        var dashIndex = value.LastIndexOf('-');
        if (dashIndex > 0 && int.TryParse(value[(dashIndex + 1)..], out var partCount))
        {
            IsMultipart = true;
            PartCount = partCount;
        }
        else
        {
            IsMultipart = false;
            PartCount = null;
        }
    }

    /// <summary>
    /// Creates a single-part ETag from an MD5 hash.
    /// </summary>
    /// <param name="md5Hash">The MD5 hash as a hexadecimal string.</param>
    /// <returns>A new ETag instance.</returns>
    public static ETag FromSinglePart(string md5Hash)
    {
        return new ETag(md5Hash.ToLowerInvariant());
    }

    /// <summary>
    /// Creates a multipart ETag from part ETags.
    /// </summary>
    /// <param name="partETags">The list of part ETags (MD5 hashes).</param>
    /// <returns>A new ETag instance with the multipart format.</returns>
    public static ETag FromMultipart(IReadOnlyList<string> partETags)
    {
        if (partETags == null || partETags.Count == 0)
        {
            throw new ArgumentException("Part ETags cannot be null or empty.", nameof(partETags));
        }

        // Calculate MD5 of concatenated part ETags
        using var md5 = System.Security.Cryptography.MD5.Create();
        foreach (var partETag in partETags.OrderBy(p => p))
        {
            var bytes = Convert.FromHexString(partETag);
            md5.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        
        var combinedHash = Convert.ToHexString(md5.Hash).ToLowerInvariant();
        return new ETag($"{combinedHash}-{partETags.Count}");
    }

    /// <summary>
    /// Implicitly converts an ETag to its string value.
    /// </summary>
    /// <param name="eTag">The ETag to convert.</param>
    /// <returns>The ETag value as a string.</returns>
    public static implicit operator string(ETag eTag) => eTag.Value;

    /// <summary>
    /// Returns the string representation of the ETag.
    /// </summary>
    /// <returns>The ETag value.</returns>
    public override string ToString() => Value;
}
