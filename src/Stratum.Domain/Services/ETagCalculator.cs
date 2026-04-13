namespace Stratum.Domain.Services;

using System.Buffers;
using System.Security.Cryptography;

/// <summary>
/// Calculates S3 ETags (MD5 hashes for single-part, combined hash for multipart).
/// </summary>
public sealed class ETagCalculator
{
    private const int BufferSize = 81920; // 80KB buffer size for optimal performance

    /// <summary>
    /// Calculates single-part ETag from stream.
    /// </summary>
    public async Task<string> CalculateSinglePartETagAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        using var md5 = MD5.Create();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return Convert.ToHexString(md5.Hash).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Calculates single-part ETag from bytes.
    /// </summary>
    public string CalculateSinglePartETag(byte[] content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(content);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates multipart ETag from part hashes (format: "hash-partCount").
    /// </summary>
    public string CalculateMultipartETag(IReadOnlyList<string> partETags)
    {
        if (partETags == null || partETags.Count == 0)
        {
            throw new ArgumentException("Part ETags cannot be null or empty.", nameof(partETags));
        }

        using var md5 = MD5.Create();

        // Concatenate all part ETags (as bytes) and calculate MD5
        foreach (var partETag in partETags.OrderBy(p => p))
        {
            var bytes = Convert.FromHexString(partETag);
            md5.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var hash = md5.Hash;
        var combinedHash = hash != null ? Convert.ToHexString(hash).ToLowerInvariant() : string.Empty;
        return $"{combinedHash}-{partETags.Count}";
    }

    /// <summary>
    /// Validates ETag format.
    /// </summary>
    public static bool IsValidETag(string eTag)
    {
        if (string.IsNullOrWhiteSpace(eTag))
        {
            return false;
        }

        // Check for multipart format: "hash-partCount"
        var dashIndex = eTag.LastIndexOf('-');
        if (dashIndex > 0)
        {
            var hashPart = eTag[..dashIndex];
            var countPart = eTag[(dashIndex + 1)..];

            // Validate hash part (hex string)
            if (hashPart.Length != 32 || !hashPart.All(IsHexDigit))
            {
                return false;
            }

            // Validate count part (integer)
            return int.TryParse(countPart, out _);
        }

        // Single-part format: just the hash
        return eTag.Length == 32 && eTag.All(IsHexDigit);
    }

    /// <summary>
    /// Checks if character is hex digit.
    /// </summary>
    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
