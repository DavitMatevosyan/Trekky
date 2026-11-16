using Microsoft.EntityFrameworkCore;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Persistence;

namespace Trekky.Modules.WorkItems.Infrastructure;

internal sealed class WorkItemDirectory(WorkItemsDbContext db) : IWorkItemDirectory
{
    public Task<bool> ExistsAsync(Guid workItemId, CancellationToken cancellationToken)
        => db.WorkItems.AsNoTracking().AnyAsync(w => w.Id == workItemId, cancellationToken);
}
