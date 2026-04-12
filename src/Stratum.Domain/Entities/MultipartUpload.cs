namespace Stratum.Domain.Entities;

/// <summary>
/// Multipart upload in progress.
/// </summary>
public class MultipartUpload
{
    /// <summary>
    /// Upload ID.
    /// </summary>
    public string UploadId { get; }

    /// <summary>
    /// Bucket name.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Gets the key of the object being uploaded.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Initiated timestamp.
    /// </summary>
    public DateTime InitiatedAt { get; }

    /// <summary>
    /// Owner ID.
    /// </summary>
    public string? OwnerId { get; }

    /// <summary>
    /// Owner display name.
    /// </summary>
    public string? OwnerDisplayName { get; }

    /// <summary>
    /// Content type.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// User metadata.
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; }

    /// <summary>
    /// Storage class.
    /// </summary>
    public string StorageClass { get; }

    /// <summary>
    /// Uploaded parts.
    /// </summary>
    public List<MultipartPart> Parts { get; }

    /// <summary>
    /// Creates multipart upload.
    /// </summary>
    public MultipartUpload(
        string uploadId,
        string bucketName,
        string key,
        string? ownerId = null,
        string? ownerDisplayName = null,
        string? contentType = null,
        Dictionary<string, string>? userMetadata = null,
        string storageClass = "STANDARD")
    {
        UploadId = uploadId ?? throw new ArgumentNullException(nameof(uploadId));
        BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        OwnerId = ownerId;
        OwnerDisplayName = ownerDisplayName;
        ContentType = contentType;
        UserMetadata = userMetadata ?? new Dictionary<string, string>();
        StorageClass = storageClass ?? "STANDARD";
        InitiatedAt = DateTime.UtcNow;
        Parts = new List<MultipartPart>();
    }

    /// <summary>
    /// Reconstructs from storage.
    /// </summary>
    public MultipartUpload(
        string uploadId,
        string bucketName,
        string key,
        DateTime initiatedAt,
        string? ownerId,
        string? ownerDisplayName,
        string? contentType,
        Dictionary<string, string> userMetadata,
        string storageClass,
        List<MultipartPart> parts)
    {
        UploadId = uploadId;
        BucketName = bucketName;
        Key = key;
        InitiatedAt = initiatedAt;
        OwnerId = ownerId;
        OwnerDisplayName = ownerDisplayName;
        ContentType = contentType;
        UserMetadata = userMetadata;
        StorageClass = storageClass;
        Parts = parts;
    }

    /// <summary>
    /// Adds a part.
    /// </summary>
    public void AddPart(MultipartPart part)
    {
        if (part == null)
        {
            throw new ArgumentNullException(nameof(part));
        }

        // Remove existing part with the same part number if it exists
        Parts.RemoveAll(p => p.PartNumber == part.PartNumber);
        Parts.Add(part);
    }

    /// <summary>
    /// Gets part by number.
    /// </summary>
    public MultipartPart? GetPart(int partNumber)
    {
        return Parts.FirstOrDefault(p => p.PartNumber == partNumber);
    }

    /// <summary>
    /// Gets sorted parts.
    /// </summary>
    public List<MultipartPart> GetSortedParts()
    {
        return Parts.OrderBy(p => p.PartNumber).ToList();
    }
}

/// <summary>
/// Multipart upload part.
/// </summary>
public class MultipartPart
{
    /// <summary>
    /// Part number (1-10000).
    /// </summary>
    public int PartNumber { get; }

    /// <summary>
    /// Part ETag (MD5).
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Part size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Upload timestamp.
    /// </summary>
    public DateTime LastModified { get; }

    /// <summary>
    /// Creates part.
    /// </summary>
    public MultipartPart(int partNumber, string eTag, long size)
    {
        if (partNumber < 1 || partNumber > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(partNumber), "Part number must be between 1 and 10000.");
        }

        PartNumber = partNumber;
        ETag = eTag ?? throw new ArgumentNullException(nameof(eTag));
        Size = size;
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconstructs from storage.
    /// </summary>
    public MultipartPart(int partNumber, string eTag, long size, DateTime lastModified)
    {
        PartNumber = partNumber;
        ETag = eTag;
        Size = size;
        LastModified = lastModified;
    }
}
