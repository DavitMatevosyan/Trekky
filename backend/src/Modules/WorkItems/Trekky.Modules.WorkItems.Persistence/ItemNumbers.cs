using Microsoft.EntityFrameworkCore;

namespace Trekky.Modules.WorkItems.Persistence;

internal static class ItemNumbers
{
    /// <summary>
    /// Takes the project's next item number (US-06 AC2). The first item creates the counter; later ones increment it.
    /// The row stays locked until the caller's transaction ends, so concurrent creates get distinct numbers, and a
    /// rolled-back create gives its number back instead of leaving a gap.
    /// </summary>
    public static async Task<int> TakeNextAsync(
        this WorkItemsDbContext db, Guid projectId, Guid orgId, CancellationToken cancellationToken)
    {
        // ToListAsync, not SingleAsync: composing a query over INSERT ... RETURNING is not valid SQL.
        var numbers = await db.Database
            .SqlQuery<int>($"""
                INSERT INTO work_items.item_counters (project_id, org_id, next_number) VALUES ({projectId}, {orgId}, 2)
                ON CONFLICT (project_id) DO UPDATE SET next_number = item_counters.next_number + 1
                RETURNING next_number - 1 AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return numbers.Single();
    }
}
