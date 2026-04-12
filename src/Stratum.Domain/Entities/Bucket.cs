namespace Stratum.Domain.Entities;

/// <summary>
/// S3 bucket for storing objects.
/// </summary>
public class Bucket
{
    /// <summary>
    /// DNS-compliant bucket name (lowercase, 3-63 chars, alphanumeric with hyphens).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Owner account/user ID.
    /// </summary>
    public string? OwnerId { get; }

    /// <summary>
    /// Owner display name.
    /// </summary>
    public string? OwnerDisplayName { get; }

    /// <summary>
    /// Hosting region (e.g., "us-east-1").
    /// </summary>
    public string Region { get; }

    /// <summary>
    /// Object count (cached).
    /// </summary>
    public long ObjectCount { get; private set; }

    /// <summary>
    /// Total size in bytes (cached).
    /// </summary>
    public long TotalSize { get; private set; }

    /// <summary>
    /// Creates a new bucket.
    /// </summary>
    public Bucket(string name, string region, string? ownerId = null, string? ownerDisplayName = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Region = region ?? throw new ArgumentNullException(nameof(region));
        OwnerId = ownerId;
        OwnerDisplayName = ownerDisplayName;
        CreatedAt = DateTime.UtcNow;
        ObjectCount = 0;
        TotalSize = 0;
    }

    /// <summary>
    /// Reconstructs a bucket from storage.
    /// </summary>
    public Bucket(
        string name,
        DateTime createdAt,
        string region,
        string? ownerId,
        string? ownerDisplayName,
        long objectCount,
        long totalSize)
    {
        Name = name;
        CreatedAt = createdAt;
        Region = region;
        OwnerId = ownerId;
        OwnerDisplayName = ownerDisplayName;
        ObjectCount = objectCount;
        TotalSize = totalSize;
    }

    /// <summary>
    /// Updates object count and total size.
    /// </summary>
    public void UpdateStatistics(long objectCountDelta, long sizeDelta)
    {
        ObjectCount = Math.Max(0, ObjectCount + objectCountDelta);
        TotalSize = Math.Max(0, TotalSize + sizeDelta);
    }
}
