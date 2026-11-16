using Microsoft.EntityFrameworkCore;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Persistence;

/// <summary>
/// Row locks that make the parent rules hold under concurrent requests (US-06 AC4, US-07 AC2). An edit locks its item
/// <c>FOR UPDATE</c> before checking its children; a create or re-parent locks the parent <c>FOR SHARE</c> before
/// checking its type. The two conflict, so a type change and a new child of that item cannot both pass their checks.
/// Locks last until the caller's transaction ends and do not change <c>xmin</c>, so ETags are unaffected.
/// </summary>
internal static class ItemLocks
{
    /// <summary>The item, tracked and locked for an edit; <c>null</c> when it does not exist or is another organization's.</summary>
    public static async Task<WorkItem?> FindForUpdateAsync(this WorkItemsDbContext db, Guid id, CancellationToken cancellationToken)
        // xmin by name: SELECT * leaves out system columns. ToListAsync: composing FirstOrDefault would wrap the SQL in a subquery.
        => (await db.WorkItems
                .FromSql($"SELECT *, xmin FROM work_items.work_items WHERE id = {id} FOR UPDATE")
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

    /// <summary>An item of the project, locked so its type cannot change before the caller commits; <c>null</c> when there is none.</summary>
    public static async Task<WorkItem?> FindParentForShareAsync(
        this WorkItemsDbContext db, Guid id, Guid projectId, CancellationToken cancellationToken)
        => (await db.WorkItems
                .FromSql($"SELECT *, xmin FROM work_items.work_items WHERE id = {id} AND project_id = {projectId} FOR SHARE")
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

    /// <summary>A comment that is not deleted, tracked and locked for an edit or delete; <c>null</c> when there is none.</summary>
    public static async Task<WorkItemComment?> FindCommentForUpdateAsync(
        this WorkItemsDbContext db, Guid id, CancellationToken cancellationToken)
        => (await db.Comments
                .FromSql($"SELECT *, xmin FROM work_items.comments WHERE id = {id} AND deleted_at IS NULL FOR UPDATE")
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

    /// <summary>
    /// Serializes board placement in a project (US-11 AC4): every write of a rank, and so of a status, takes the
    /// project's counter row first, so two cards can never be given the same place. The counter exists once the project
    /// has an item, which any card being placed is. Lock order: the item first, then this.
    /// </summary>
    public static Task LockBoardAsync(this WorkItemsDbContext db, Guid projectId, CancellationToken cancellationToken)
        => db.Database
            .SqlQuery<int>($"SELECT 1 AS \"Value\" FROM work_items.item_counters WHERE project_id = {projectId} FOR UPDATE")
            .ToListAsync(cancellationToken);
}
