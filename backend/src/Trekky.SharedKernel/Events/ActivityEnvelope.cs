using System.Text.Json.Nodes;

namespace Trekky.SharedKernel.Events;

/// <summary>
/// One activity event as it travels from a module's outbox to the activity log (docs/tech/activity-events.md).
/// Stable and self-describing: later consumers (notifications, webhooks, the AI context layer) read the same shape.
/// </summary>
public sealed record ActivityEnvelope(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid OrgId,
    EventActor Actor,
    EventEntity Entity,
    Guid? ProjectId,
    JsonObject Payload)
{
    /// <summary>A new event with a UUIDv7 id (ordered by time) and schema version 1.</summary>
    public static ActivityEnvelope Create(
        string eventType,
        DateTimeOffset occurredAt,
        Guid orgId,
        EventActor actor,
        EventEntity entity,
        Guid? projectId,
        JsonObject payload)
        => new(Guid.CreateVersion7(occurredAt), eventType, 1, occurredAt, orgId, actor, entity, projectId, payload);
}
