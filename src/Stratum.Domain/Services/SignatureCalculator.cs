namespace Stratum.Domain.Services;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Provides methods for calculating cryptographic signatures used in AWS authentication.
/// This includes HMAC-SHA256 signatures for Signature Version 4.
/// </summary>
public sealed class SignatureCalculator
{
    /// <summary>
    /// Calculates an HMAC-SHA256 signature.
    /// </summary>
    /// <param name="key">The signing key.</param>
    /// <param name="data">The data to sign.</param>
    /// <returns>The HMAC-SHA256 signature as a hexadecimal string.</returns>
    public static string CalculateHmacSha256(string key, string data)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates an HMAC-SHA256 signature from byte arrays.
    /// </summary>
    /// <param name="key">The signing key as bytes.</param>
    /// <param name="data">The data to sign as bytes.</param>
    /// <returns>The HMAC-SHA256 signature as a hexadecimal string.</returns>
    public static string CalculateHmacSha256(byte[] key, byte[] data)
    {
        if (key == null || key.Length == 0)
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        }

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates the signing key for AWS Signature Version 4.
    /// </summary>
    /// <param name="secretKey">The secret access key.</param>
    /// <param name="date">The date string in YYYYMMDD format.</param>
    /// <param name="region">The AWS region.</param>
    /// <param name="service">The AWS service name (e.g., "s3").</param>
    /// <returns>The derived signing key as bytes.</returns>
    public static byte[] CalculateSigningKey(string secretKey, string date, string region, string service)
    {
        if (string.IsNullOrEmpty(secretKey))
        {
            throw new ArgumentException("Secret key cannot be null or empty.", nameof(secretKey));
        }

        if (string.IsNullOrEmpty(date))
        {
            throw new ArgumentException("Date cannot be null or empty.", nameof(date));
        }

        if (string.IsNullOrEmpty(region))
        {
            throw new ArgumentException("Region cannot be null or empty.", nameof(region));
        }

        if (string.IsNullOrEmpty(service))
        {
            throw new ArgumentException("Service cannot be null or empty.", nameof(service));
        }

        // AWS4 secret key prefix
        var kSecret = Encoding.UTF8.GetBytes($"AWS4{secretKey}");

        // Derive keys: kDate -> kRegion -> kService -> kSigning
        var kDate = HmacSha256(kSecret, date);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        var kSigning = HmacSha256(kService, "aws4_request");

        return kSigning;
    }

    /// <summary>
    /// Calculates the signature string for AWS Signature Version 4.
    /// </summary>
    /// <param name="signingKey">The signing key.</param>
    /// <param name="stringToSign">The string to sign.</param>
    /// <returns>The signature as a hexadecimal string.</returns>
    public static string CalculateSignature(byte[] signingKey, string stringToSign)
    {
        if (signingKey == null || signingKey.Length == 0)
        {
            throw new ArgumentException("Signing key cannot be null or empty.", nameof(signingKey));
        }

        if (string.IsNullOrEmpty(stringToSign))
        {
            throw new ArgumentException("String to sign cannot be null or empty.", nameof(stringToSign));
        }

        using var hmac = new HMACSHA256(signingKey);
        var stringToSignBytes = Encoding.UTF8.GetBytes(stringToSign);
        var signature = hmac.ComputeHash(stringToSignBytes);

        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates a SHA256 hash of the given data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The SHA256 hash as a hexadecimal string.</returns>
    public static string CalculateSha256(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        }

        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(dataBytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Calculates a SHA256 hash of the given byte array.
    /// </summary>
    /// <param name="data">The data to hash as bytes.</param>
    /// <returns>The SHA256 hash as a hexadecimal string.</returns>
    public static string CalculateSha256(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Helper method to calculate HMAC-SHA256 and return bytes.
    /// </summary>
    private static byte[] HmacSha256(byte[] key, string data)
    {
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(dataBytes);
    }
}
