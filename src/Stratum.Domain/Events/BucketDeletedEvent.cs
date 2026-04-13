namespace Stratum.Domain.Events;

/// <summary>
/// Event raised when a bucket is deleted.
/// </summary>
public sealed class BucketDeletedEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the bucket was deleted.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the name of the bucket that was deleted.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Initializes a new instance of the BucketDeletedEvent class.
    /// </summary>
    /// <param name="bucketName">The bucket name.</param>
    public BucketDeletedEvent(string bucketName)
    {
        BucketName = bucketName;
        OccurredAt = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}
