namespace Trekky.SharedKernel.Events;

/// <summary>
/// Receives events from the outbox dispatcher, inside a scope whose tenant is the event's organization. Delivery is
/// at least once, so implementations must ignore an <see cref="ActivityEnvelope.EventId"/> they have already stored.
/// The activity module implements it; notifications and webhooks will later.
/// </summary>
public interface IActivityEventSink
{
    Task WriteAsync(ActivityEnvelope envelope, CancellationToken cancellationToken);
}
