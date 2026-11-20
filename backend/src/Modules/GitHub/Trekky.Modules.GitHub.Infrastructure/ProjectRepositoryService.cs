using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.GitHub;
using Trekky.Modules.GitHub.Application.Repositories;
using Trekky.Modules.GitHub.Domain;
using Trekky.Modules.GitHub.Persistence;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;

namespace Trekky.Modules.GitHub.Infrastructure;

internal sealed class ProjectRepositoryService(
    GitHubDbContext db,
    ITenantContext tenant,
    TimeProvider time,
    IGitHubApp app,
    IGitHubClient github,
    IProjectDirectory projects) : IProjectRepositoryService
{
    public async Task<VersionedProjectRepositoryLink?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var row = await db.ProjectRepositories.AsNoTracking()
            .Where(l => l.ProjectId == projectId)
            .Join(db.Installations.AsNoTracking(), l => l.InstallationRowId, i => i.Id, (link, installation) => new { link, installation })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToVersioned(row.link, row.installation);
    }

    public async Task<LinkRepositoryResult> LinkAsync(
        Guid projectId, LinkProjectRepositoryRequest request, string? ifMatch, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!app.IsConfigured)
        {
            return new(LinkRepositoryOutcome.NotConfigured);
        }

        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Linking a repository needs a tenant.");
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new(LinkRepositoryOutcome.ProjectNotFound);
        }

        var installation = await db.Installations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InstallationId, cancellationToken);
        if (installation is null)
        {
            return new(LinkRepositoryOutcome.ConnectionNotFound);
        }

        var existing = await db.ProjectRepositories.FirstOrDefaultAsync(l => l.ProjectId == projectId, cancellationToken);
        if (existing is not null && string.IsNullOrEmpty(ifMatch))
        {
            return new(LinkRepositoryOutcome.IfMatchRequired);
        }

        if (!string.IsNullOrEmpty(ifMatch) && (existing is null || !VersionedProjectRepositoryLink.IfMatchAccepts(ifMatch, existing.Version)))
        {
            return new(LinkRepositoryOutcome.Stale, await GetAsync(projectId, cancellationToken));
        }

        var docsPath = DocsPath.Normalize(request.DocsPath ?? ProjectRepository.DefaultDocsPath)!;
        var branch = (request.Branch ?? ProjectRepository.DefaultBranch).Trim();

        GitHubRepositoryInfo? repository;
        try
        {
            // The installation's own list is the proof of access: a public repository outside the app's selection
            // would answer GET /repos too, but Trekky could not open pull requests there.
            var (repositories, _) = await github.ListRepositoriesAsync(
                installation.InstallationId, GitHubConnectionService.MaxRepositories, cancellationToken);
            repository = repositories.FirstOrDefault(r => string.Equals(r.FullName, request.Repository, StringComparison.OrdinalIgnoreCase));
            if (repository is null)
            {
                return new(LinkRepositoryOutcome.RepositoryNotFound);
            }

            if (!await github.BranchExistsAsync(installation.InstallationId, repository.FullName, branch, cancellationToken))
            {
                return new(LinkRepositoryOutcome.BranchNotFound);
            }

            var kind = await github.GetPathKindAsync(
                installation.InstallationId, repository.FullName, branch, docsPath.TrimEnd('/'), cancellationToken);
            if (kind != GitHubPathKind.Folder)
            {
                return new(LinkRepositoryOutcome.DocsFolderNotFound);
            }
        }
        catch (GitHubInstallationGoneException)
        {
            return new(LinkRepositoryOutcome.InstallationGone);
        }

        var now = time.GetUtcNow();
        var created = existing is null;
        var link = existing ?? new ProjectRepository { Id = Guid.CreateVersion7(now), OrgId = orgId, ProjectId = projectId };
        if (!created && link.InstallationRowId == installation.Id && link.RepositoryId == repository.Id
            && link.RepositoryFullName == repository.FullName && link.DocsPath == docsPath && link.Branch == branch)
        {
            // Saved again unchanged: no write, no event.
            return new(LinkRepositoryOutcome.Updated, ToVersioned(link, installation));
        }

        var previous = created ? null : ProjectRepositoryEvents.Settings(link);
        link.InstallationRowId = installation.Id;
        link.RepositoryId = repository.Id;
        link.RepositoryFullName = repository.FullName;
        link.DocsPath = docsPath;
        link.Branch = branch;
        link.LinkedBy = actorId;
        link.LinkedAt = now;

        if (created)
        {
            db.ProjectRepositories.Add(link);
            db.AddActivityEvent(ProjectRepositoryEvents.Linked(link, project.Key, actorId, now));
        }
        else
        {
            db.AddActivityEvent(ProjectRepositoryEvents.Changed(link, previous!, project.Key, actorId, now));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException
            || exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: GitHubDbContext.ProjectIndexName } })
        {
            // Another request saved the project's link first.
            db.ChangeTracker.Clear();
            return new(LinkRepositoryOutcome.Stale, await GetAsync(projectId, cancellationToken));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // The connection was disconnected while GitHub was being asked.
            db.ChangeTracker.Clear();
            return new(LinkRepositoryOutcome.ConnectionNotFound);
        }

        // The new xmin is only known after the write.
        var saved = await GetAsync(projectId, cancellationToken);
        return new(created ? LinkRepositoryOutcome.Created : LinkRepositoryOutcome.Updated, saved);
    }

    public async Task<bool> UnlinkAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var link = await db.ProjectRepositories.FirstOrDefaultAsync(l => l.ProjectId == projectId, cancellationToken);
        if (link is null)
        {
            return false;
        }

        var project = await projects.FindAsync(projectId, cancellationToken);
        db.ProjectRepositories.Remove(link);
        db.AddActivityEvent(ProjectRepositoryEvents.Unlinked(
            link, project?.Key, link.OrgId, actorId, time.GetUtcNow(), ProjectRepositoryEvents.UnlinkedReason));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static VersionedProjectRepositoryLink ToVersioned(ProjectRepository link, GitHubInstallation installation)
        => new(
            new ProjectRepositoryLink(
                link.ProjectId,
                installation.Id,
                installation.AccountLogin,
                link.RepositoryId,
                link.RepositoryFullName,
                link.DocsPath,
                link.Branch,
                GitHubUrls.Folder(link.RepositoryFullName, link.Branch, link.DocsPath),
                link.LinkedAt),
            link.Version);
}
