namespace Stratum.Domain.Events;

/// <summary>
/// Event raised when an object is deleted from a bucket.
/// </summary>
public sealed class ObjectDeletedEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the object was deleted.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the name of the bucket where the object was deleted.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Gets the key of the object that was deleted.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the version ID of the deleted object, if versioning is enabled.
    /// </summary>
    public string? VersionId { get; }

    /// <summary>
    /// Gets whether this was a delete marker (when versioning is enabled).
    /// </summary>
    public bool IsDeleteMarker { get; }

    /// <summary>
    /// Initializes a new instance of the ObjectDeletedEvent class.
    /// </summary>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="key">The object key.</param>
    /// <param name="versionId">Optional version ID.</param>
    /// <param name="isDeleteMarker">Whether this is a delete marker.</param>
    public ObjectDeletedEvent(
        string bucketName,
        string key,
        string? versionId = null,
        bool isDeleteMarker = false)
    {
        BucketName = bucketName;
        Key = key;
        VersionId = versionId;
        IsDeleteMarker = isDeleteMarker;
        OccurredAt = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}
