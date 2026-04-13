namespace Stratum.Domain.Entities;

/// <summary>
/// Object metadata (content excluded).
/// </summary>
public class ObjectMetadata
{
    /// <summary>
    /// Bucket name.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Object key (supports / for directories).
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// ETag (MD5 hash).
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Last modified timestamp.
    /// </summary>
    public DateTime LastModified { get; }

    /// <summary>
    /// Content type (MIME).
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Content encoding (e.g., gzip).
    /// </summary>
    public string? ContentEncoding { get; }

    /// <summary>
    /// Gets the content disposition of the object.
    /// </summary>
    public string? ContentDisposition { get; }

    /// <summary>
    /// Gets the content language of the object.
    /// </summary>
    public string? ContentLanguage { get; }

    /// <summary>
    /// Gets the cache control directive for the object.
    /// </summary>
    public string? CacheControl { get; }

    /// <summary>
    /// Custom metadata.
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; }

    /// <summary>
    /// Storage class.
    /// </summary>
    public string StorageClass { get; }

    /// <summary>
    /// Version ID (if versioning enabled).
    /// </summary>
    public string? VersionId { get; }

    /// <summary>
    /// Is latest version.
    /// </summary>
    public bool IsLatest { get; }

    /// <summary>
    /// Is delete marker.
    /// </summary>
    public bool IsDeleteMarker { get; }

    /// <summary>
    /// Creates object metadata.
    /// </summary>
    public ObjectMetadata(
        string bucketName,
        string key,
        string eTag,
        long size,
        string contentType,
        DateTime lastModified)
    {
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ETag = eTag ?? throw new ArgumentNullException(nameof(eTag));
        Size = size;
        ContentType = contentType ?? "application/octet-stream";
        LastModified = lastModified;
        UserMetadata = new Dictionary<string, string>();
        StorageClass = "STANDARD";
        IsLatest = true;
        IsDeleteMarker = false;
    }

    /// <summary>
    /// Creates object metadata with all properties.
    /// </summary>
    public ObjectMetadata(
        string bucketName,
        string key,
        string eTag,
        long size,
        string contentType,
        string? contentEncoding,
        string? contentDisposition,
        string? contentLanguage,
        string? cacheControl,
        Dictionary<string, string> userMetadata,
        string storageClass,
        string? versionId,
        bool isLatest,
        bool isDeleteMarker,
        DateTime lastModified)
    {
        BucketName = bucketName;
        Key = key;
        ETag = eTag;
        Size = size;
        ContentType = contentType;
        ContentEncoding = contentEncoding;
        ContentDisposition = contentDisposition;
        ContentLanguage = contentLanguage;
        CacheControl = cacheControl;
        UserMetadata = userMetadata ?? new Dictionary<string, string>();
        StorageClass = storageClass ?? "STANDARD";
        VersionId = versionId;
        IsLatest = isLatest;
        IsDeleteMarker = isDeleteMarker;
        LastModified = lastModified;
    }

    /// <summary>
    /// Sets user metadata.
    /// </summary>
    public void SetUserMetadata(string key, string value)
    {
        UserMetadata[key] = value;
    }

    /// <summary>
    /// Gets user metadata value.
    /// </summary>
    public string? GetUserMetadata(string key)
    {
        return UserMetadata.TryGetValue(key, out var value) ? value : null;
    }
}
