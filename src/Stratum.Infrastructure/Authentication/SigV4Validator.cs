namespace Stratum.Infrastructure.Authentication;

using Stratum.Domain.Entities;
using Stratum.Domain.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Validates AWS Signature Version 4 signatures for S3 requests.
/// Implements the canonical request construction and signature verification according to AWS specification.
/// </summary>
public sealed class SigV4Validator
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private const string Terminator = "aws4_request";
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Validates an AWS Signature Version 4 signed request.
    /// </summary>
    /// <param name="accessKey">The access key for signature verification.</param>
    /// <param name="method">The HTTP method (GET, PUT, POST, DELETE, HEAD).</param>
    /// <param name="uri">The request URI path.</param>
    /// <param name="queryString">The query string (without the leading ?).</param>
    /// <param name="headers">The request headers.</param>
    /// <param name="payloadHash">The SHA256 hash of the request payload.</param>
    /// <param name="authorizationHeader">The Authorization header value.</param>
    /// <param name="timestamp">The request timestamp (X-Amz-Date header).</param>
    /// <param name="region">The AWS region.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    public bool Validate(
        AccessKey accessKey,
        string method,
        string uri,
        string queryString,
        IDictionary<string, string> headers,
        string payloadHash,
        string authorizationHeader,
        string timestamp,
        string region)
    {
        try
        {
            // Validate timestamp (clock skew check)
            if (!ValidateTimestamp(timestamp))
            {
                return false;
            }

            // Parse Authorization header
            var authParts = ParseAuthorizationHeader(authorizationHeader);
            if (authParts == null)
            {
                return false;
            }

            // Verify algorithm
            if (authParts.Algorithm != Algorithm)
            {
                return false;
            }

            // Verify credential
            var credentialParts = authParts.Credential.Split('/');
            if (credentialParts.Length != 4)
            {
                return false;
            }

            var credentialDate = credentialParts[0];
            var credentialRegion = credentialParts[1];
            var credentialService = credentialParts[2];

            if (credentialRegion != region || credentialService != Service)
            {
                return false;
            }

            // Build canonical request
            var canonicalRequest = BuildCanonicalRequest(
                method,
                uri,
                queryString,
                headers,
                payloadHash);

            var canonicalRequestHash = SignatureCalculator.CalculateSha256(canonicalRequest);

            // Build string to sign
            var stringToSign = BuildStringToSign(
                timestamp,
                credentialRegion,
                canonicalRequestHash);

            // Calculate signing key
            var signingKey = SignatureCalculator.CalculateSigningKey(
                accessKey.SecretAccessKeyHash,
                credentialDate,
                credentialRegion,
                Service);

            // Calculate signature
            var calculatedSignature = SignatureCalculator.CalculateSignature(signingKey, stringToSign);

            // Compare signatures
            return calculatedSignature.Equals(authParts.Signature, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the timestamp is within acceptable clock skew.
    /// </summary>
    private bool ValidateTimestamp(string timestamp)
    {
        if (!DateTime.TryParseExact(timestamp, "yyyyMMddTHHmmssZ", null, DateTimeStyles.AssumeUniversal, out var requestTime))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var difference = now - requestTime;

        return Math.Abs(difference.TotalMinutes) <= MaxClockSkew.TotalMinutes;
    }

    /// <summary>
    /// Parses the Authorization header into its components.
    /// </summary>
    private AuthorizationParts? ParseAuthorizationHeader(string authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        var parts = authorizationHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        var result = new AuthorizationParts();
        
        foreach (var part in parts)
        {
            if (part.StartsWith("AWS4-HMAC-SHA256", StringComparison.OrdinalIgnoreCase))
            {
                var algorithmAndCredential = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (algorithmAndCredential.Length >= 2)
                {
                    result.Algorithm = algorithmAndCredential[0];
                    result.Credential = algorithmAndCredential[1].Replace("Credential=", string.Empty);
                }
            }
            else if (part.StartsWith("SignedHeaders=", StringComparison.OrdinalIgnoreCase))
            {
                result.SignedHeaders = part.Replace("SignedHeaders=", string.Empty);
            }
            else if (part.StartsWith("Signature=", StringComparison.OrdinalIgnoreCase))
            {
                result.Signature = part.Replace("Signature=", string.Empty);
            }
        }

        if (string.IsNullOrEmpty(result.Algorithm) || string.IsNullOrEmpty(result.Credential) ||
            string.IsNullOrEmpty(result.SignedHeaders) || string.IsNullOrEmpty(result.Signature))
        {
            return null;
        }

        return result;
    }

    /// <summary>
    /// Builds the canonical request according to AWS specification.
    /// </summary>
    private string BuildCanonicalRequest(
        string method,
        string uri,
        string queryString,
        IDictionary<string, string> headers,
        string payloadHash)
    {
        var sb = new StringBuilder();

        // HTTP method
        sb.AppendLine(method.ToUpperInvariant());

        // Canonical URI
        sb.AppendLine(UriEncode(uri));

        // Canonical query string
        sb.AppendLine(BuildCanonicalQueryString(queryString));

        // Canonical headers
        var signedHeaders = headers.Keys
            .Select(k => k.ToLowerInvariant())
            .OrderBy(k => k)
            .ToList();

        foreach (var header in signedHeaders)
        {
            var headerValue = headers[header];
            sb.AppendLine($"{header}:{headerValue.Trim()}");
        }

        sb.AppendLine();

        // Signed headers
        sb.AppendLine(string.Join(";", signedHeaders));

        // Payload hash
        sb.Append(payloadHash);

        return sb.ToString();
    }

    /// <summary>
    /// Builds the canonical query string with parameters sorted alphabetically.
    /// </summary>
    private string BuildCanonicalQueryString(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return string.Empty;
        }

        var parameters = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sortedParams = parameters
            .Select(p =>
            {
                var keyValue = p.Split('=');
                var key = UriEncode(keyValue[0]);
                var value = keyValue.Length > 1 ? UriEncode(keyValue[1]) : string.Empty;
                return $"{key}={value}";
            })
            .OrderBy(p => p)
            .ToList();

        return string.Join("&", sortedParams);
    }

    /// <summary>
    /// URI-encodes a string according to AWS specification.
    /// </summary>
    private string UriEncode(string value)
    {
        var sb = new StringBuilder();

        foreach (var c in value)
        {
            if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                sb.Append(c);
            }
            else if (c == ' ')
            {
                sb.Append('+');
            }
            else
            {
                sb.Append('%');
                sb.Append(Convert.ToHexString(new[] { (byte)c }));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the string to sign for signature calculation.
    /// </summary>
    private string BuildStringToSign(string timestamp, string region, string canonicalRequestHash)
    {
        var credentialScope = $"{timestamp[..8]}/{region}/{Service}/{Terminator}";
        
        return $"{Algorithm}\n{timestamp}\n{credentialScope}\n{canonicalRequestHash}";
    }

    /// <summary>
    /// Represents the parsed components of an Authorization header.
    /// </summary>
    private sealed class AuthorizationParts
    {
        public string Algorithm { get; set; } = string.Empty;
        public string Credential { get; set; } = string.Empty;
        public string SignedHeaders { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
