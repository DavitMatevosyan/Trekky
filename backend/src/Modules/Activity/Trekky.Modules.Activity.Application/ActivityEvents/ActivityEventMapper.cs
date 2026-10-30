using Trekky.Modules.Activity.Domain;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Application.ActivityEvents;

/// <summary>Turns an event envelope from the outbox into an activity log row.</summary>
internal static class ActivityEventMapper
{
    public static ActivityEvent FromEnvelope(ActivityEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new ActivityEvent
        {
            EventId = envelope.EventId,
            OrgId = envelope.OrgId,
            EventType = envelope.EventType,
            SchemaVersion = envelope.SchemaVersion,
            OccurredAt = envelope.OccurredAt,
            ActorType = envelope.Actor.Type,
            ActorId = envelope.Actor.Id,
            EntityType = envelope.Entity.Type,
            EntityId = envelope.Entity.Id,
            EntityKey = envelope.Entity.Key,
            ProjectId = envelope.ProjectId,
            Payload = envelope.Payload.ToJsonString(),
        };
    }
}
