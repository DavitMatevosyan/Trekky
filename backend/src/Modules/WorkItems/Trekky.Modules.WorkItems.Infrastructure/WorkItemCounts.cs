using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.WorkItems.Persistence;

namespace Trekky.Modules.WorkItems.Infrastructure;

/// <summary>Item counts for the projects list (US-05 AC3), from this module's own table.</summary>
internal sealed class WorkItemCounts(WorkItemsDbContext db) : IProjectItemCounts
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await db.WorkItems
            .AsNoTracking()
            .Where(w => projectIds.Contains(w.ProjectId))
            .GroupBy(w => w.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProjectId, g => g.Count, cancellationToken);
    }
}
