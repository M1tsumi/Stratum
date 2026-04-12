namespace Stratum.Domain.Events;

/// <summary>
/// Event raised when a multipart upload is successfully completed.
/// </summary>
public sealed class MultipartUploadCompletedEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the multipart upload was completed.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the upload ID of the completed multipart upload.
    /// </summary>
    public string UploadId { get; }

    /// <summary>
    /// Gets the name of the bucket where the object was uploaded.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Gets the key of the object that was uploaded.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the total size of the completed object in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the entity tag (ETag) of the completed object.
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Gets the number of parts in the multipart upload.
    /// </summary>
    public int PartCount { get; }

    /// <summary>
    /// Initializes a new instance of the MultipartUploadCompletedEvent class.
    /// </summary>
    /// <param name="uploadId">The upload ID.</param>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="key">The object key.</param>
    /// <param name="size">The object size in bytes.</param>
    /// <param name="eTag">The entity tag.</param>
    /// <param name="partCount">The number of parts.</param>
    public MultipartUploadCompletedEvent(
        string uploadId,
        string bucketName,
        string key,
        long size,
        string eTag,
        int partCount)
    {
        UploadId = uploadId;
        BucketName = bucketName;
        Key = key;
        Size = size;
        ETag = eTag;
        PartCount = partCount;
        OccurredAt = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}
