using System.Text.Json;
using Trekky.Modules.Activity.Application.Contracts;
using Trekky.Modules.Activity.Domain;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.Activity.Application.ActivityEvents;

/// <summary>Turns an event envelope from the outbox into an activity log row, and a row into a history entry.</summary>
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

    public static HistoryEvent ToHistoryEvent(ActivityEvent activity, string? actorDisplayName)
    {
        ArgumentNullException.ThrowIfNull(activity);
        using var payload = JsonDocument.Parse(activity.Payload);
        return new HistoryEvent(
            activity.EventId,
            activity.EventType,
            activity.SchemaVersion,
            activity.OccurredAt,
            new HistoryActor(activity.ActorType, activity.ActorId, actorDisplayName),
            new HistoryEntity(activity.EntityType, activity.EntityId, activity.EntityKey),
            payload.RootElement.Clone());
    }
}
