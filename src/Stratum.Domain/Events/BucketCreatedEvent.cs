namespace Stratum.Domain.Events;

/// <summary>
/// Event raised when a bucket is successfully created.
/// </summary>
public sealed class BucketCreatedEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the bucket was created.
    /// </summary>
    public DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// Gets the name of the bucket that was created.
    /// </summary>
    public string BucketName { get; }

    /// <summary>
    /// Gets the region where the bucket was created.
    /// </summary>
    public string Region { get; }

    /// <summary>
    /// Gets the owner ID of the bucket.
    /// </summary>
    public string? OwnerId { get; }

    /// <summary>
    /// Initializes a new instance of the BucketCreatedEvent class.
    /// </summary>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="region">The region.</param>
    /// <param name="ownerId">Optional owner ID.</param>
    public BucketCreatedEvent(
        string bucketName,
        string region,
        string? ownerId = null)
    {
        BucketName = bucketName;
        Region = region;
        OwnerId = ownerId;
        OccurredAt = DateTime.UtcNow;
        EventId = Guid.NewGuid();
    }
}
