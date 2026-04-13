namespace Stratum.Domain.Events;

/// <summary>
/// Marker interface for domain events in the Stratum system.
/// Domain events represent significant occurrences in the system that may need to be handled by subscribers.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }
}
