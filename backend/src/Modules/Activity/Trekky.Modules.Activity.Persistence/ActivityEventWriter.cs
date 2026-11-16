using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Activity.Domain;

namespace Trekky.Modules.Activity.Persistence;

/// <summary>
/// Appends to the activity log, ignoring an event id that is already there: the outbox delivers at least once.
/// Runs as the event's tenant, so RLS checks the row's <c>org_id</c>.
/// </summary>
internal sealed class ActivityEventWriter(ActivityDbContext db)
{
    public Task AppendAsync(ActivityEvent activity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO activity.activity_events
                (event_id, org_id, event_type, schema_version, occurred_at, actor_type, actor_id,
                 entity_type, entity_id, entity_key, project_id, payload)
            VALUES
                ({activity.EventId}, {activity.OrgId}, {activity.EventType}, {activity.SchemaVersion}, {activity.OccurredAt},
                 {activity.ActorType}, {activity.ActorId}, {activity.EntityType}, {activity.EntityId}, {activity.EntityKey},
                 {activity.ProjectId}, CAST({activity.Payload} AS jsonb))
            ON CONFLICT (event_id) DO NOTHING
            """,
            cancellationToken);
    }
}
