using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Modules.Projects.Application.Projects;
using Trekky.Modules.Projects.Domain;
using Trekky.Modules.Projects.Persistence;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;
using ProjectEntity = Trekky.Modules.Projects.Domain.Project;

namespace Trekky.Modules.Projects.Infrastructure;

internal sealed class ProjectService(ProjectsDbContext db, ITenantContext tenant, TimeProvider time) : IProjectService
{
    public async Task<(CreateProjectOutcome Outcome, VersionedProject? Project)> CreateAsync(
        CreateProjectRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Creating a project needs a tenant.");
        var now = time.GetUtcNow();

        var project = new ProjectEntity
        {
            Id = Guid.CreateVersion7(now),
            OrgId = orgId,
            Key = request.Key!,
            Name = request.Name!.Trim(),
            CreatedBy = actorId,
            CreatedAt = now,
        };
        db.Projects.Add(project);

        // US-05 AC4: the event commits with the project, or neither does.
        db.AddActivityEvent(ActivityEnvelope.Create(
            ProjectEventTypes.ProjectCreated,
            now,
            orgId,
            EventActor.User(actorId),
            new EventEntity(ProjectEntity.EntityType, project.Id, project.Key),
            project.Id,
            new JsonObject { ["key"] = project.Key, ["name"] = project.Name }));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ProjectsDbContext.KeyIndexName,
            })
        {
            return (CreateProjectOutcome.KeyTaken, null);
        }

        return (CreateProjectOutcome.Created, ToVersioned(project));
    }

    public async Task<ProjectPage> ListAsync(int limit, ProjectCursor? after, CancellationToken cancellationToken)
    {
        IQueryable<ProjectEntity> query = db.Projects.AsNoTracking();
        if (after is not null)
        {
            // Keyset: rows after the previous page's last (name, id), as a row-value comparison the index serves.
            query = query.Where(p => EF.Functions.GreaterThan(
                ValueTuple.Create(p.Name, p.Id), ValueTuple.Create(after.Name, after.Id)));
        }

        var rows = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? new ProjectCursor(page[^1].Name, page[^1].Id).Encode() : null;
        return new ProjectPage(page.Select(ToContract).ToList(), next);
    }

    public async Task<VersionedProject?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        return project is null ? null : ToVersioned(project);
    }

    private static VersionedProject ToVersioned(ProjectEntity project) => new(ToContract(project), project.Version);

    // Item counts arrive with work items (TRK-11).
    private static Application.Contracts.Project ToContract(ProjectEntity project)
        => new(project.Id, project.Key, project.Name, ItemCount: 0, project.CreatedAt);
}
