namespace Stratum.Domain.Events;

/// <summary>
/// Event raised when an object is successfully created in a bucket.
/// </summary>
public sealed class ObjectCreatedEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the object was created.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the name of the bucket where the object was created.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Gets the key of the object that was created.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the size of the object in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Gets the entity tag (ETag) of the object.
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Gets the content type of the object.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the custom user metadata associated with the object.
    /// </summary>
    public Dictionary<string, string> UserMetadata { get; }

    /// <summary>
    /// Gets the version ID of the object, if versioning is enabled.
    /// </summary>
    public string? VersionId { get; }

    /// <summary>
    /// Initializes a new instance of the ObjectCreatedEvent class.
    /// </summary>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="key">The object key.</param>
    /// <param name="size">The object size in bytes.</param>
    /// <param name="eTag">The entity tag.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="userMetadata">The user metadata.</param>
    /// <param name="versionId">Optional version ID.</param>
    public ObjectCreatedEvent(
        string bucketName,
        string key,
        long size,
        string eTag,
        string contentType,
        Dictionary<string, string> userMetadata,
        string? versionId = null)
    {
        BucketName = bucketName;
        Key = key;
        Size = size;
        ETag = eTag;
        ContentType = contentType;
        UserMetadata = userMetadata;
        VersionId = versionId;
        OccurredAt = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}
