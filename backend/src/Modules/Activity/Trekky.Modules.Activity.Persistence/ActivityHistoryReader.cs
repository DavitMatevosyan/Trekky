using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Activity.Domain;

namespace Trekky.Modules.Activity.Persistence;

/// <summary>Reads one entity's events, newest first, as the request's tenant (RLS limits it to that organization).</summary>
internal sealed class ActivityHistoryReader(ActivityDbContext db)
{
    /// <summary>Up to <paramref name="take"/> events of the entity older than <paramref name="before"/> (or the newest).</summary>
    public Task<List<ActivityEvent>> ReadAsync(Guid entityId, Guid? before, int take, CancellationToken cancellationToken)
    {
        var query = db.Events.AsNoTracking().Where(e => e.EntityId == entityId);
        if (before is { } eventId)
        {
            query = query.Where(e => e.EventId < eventId);
        }

        // UUIDv7 ids order by time (to the millisecond); ix_activity_events_entity serves this.
        return query.OrderByDescending(e => e.EventId).Take(take).ToListAsync(cancellationToken);
    }
}
