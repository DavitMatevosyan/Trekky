using Microsoft.EntityFrameworkCore;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.Projects.Persistence;

namespace Trekky.Modules.Projects.Infrastructure;

internal sealed class ProjectDirectory(ProjectsDbContext db) : IProjectDirectory
{
    public Task<ProjectRef?> FindAsync(Guid projectId, CancellationToken cancellationToken)
        => db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectRef(p.Id, p.Key, p.Name))
            .FirstOrDefaultAsync(cancellationToken);
}
