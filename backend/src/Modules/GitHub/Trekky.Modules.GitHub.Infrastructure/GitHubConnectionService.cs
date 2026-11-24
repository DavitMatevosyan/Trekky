using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Trekky.Modules.GitHub.Application.Connections;
using Trekky.Modules.GitHub.Application.Contracts;
using Trekky.Modules.GitHub.Application.GitHub;
using Trekky.Modules.GitHub.Domain;
using Trekky.Modules.GitHub.Persistence;
using Trekky.Modules.Projects.Application.Contracts;
using Trekky.Persistence.Outbox;
using Trekky.SharedKernel;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.GitHub.Infrastructure;

internal sealed class GitHubConnectionService(
    GitHubDbContext db,
    ITenantContext tenant,
    TimeProvider time,
    IGitHubApp app,
    IGitHubClient github,
    IProjectDirectory projects) : IGitHubConnectionService
{
    /// <summary>The repository picker lists at most this many; beyond it the name is typed (GitHub pages by 100).</summary>
    public const int MaxRepositories = 1000;

    public async Task<GitHubSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var installations = await db.Installations
            .AsNoTracking()
            .OrderBy(i => i.ConnectedAt)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);
        return new GitHubSettings(app.IsConfigured, app.InstallUrl, installations.Select(ToContract).ToList());
    }

    public async Task<(ConnectGitHubOutcome Outcome, GitHubConnection? Connection)> ConnectAsync(
        ConnectGitHubRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!app.IsConfigured)
        {
            return (ConnectGitHubOutcome.NotConfigured, null);
        }

        var orgId = tenant.OrgId ?? throw new InvalidOperationException("Connecting GitHub needs a tenant.");
        var installationId = request.InstallationId!.Value;

        // The installation_id in GitHub's redirect can be forged; the code cannot. Only an installation the user
        // behind the code can access on GitHub is connected (GitHub's own advice for the setup URL).
        var userToken = await github.ExchangeCodeAsync(request.Code!, cancellationToken);
        if (userToken is null)
        {
            return (ConnectGitHubOutcome.CodeRejected, null);
        }

        try
        {
            // Listed for the user is not enough: read access to one repository lists the whole installation. The user
            // must administer every repository it reaches, as they would to install it there themselves.
            var accessible = await github.ListUserInstallationIdsAsync(userToken, cancellationToken);
            if (!accessible.Contains(installationId)
                || !await github.UserAdministersInstallationAsync(userToken, installationId, cancellationToken))
            {
                return (ConnectGitHubOutcome.NotAccessible, null);
            }
        }
        finally
        {
            await github.RevokeUserTokenAsync(userToken, CancellationToken.None);
        }

        var existing = await db.Installations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.InstallationId == installationId, cancellationToken);
        if (existing is not null)
        {
            return (ConnectGitHubOutcome.AlreadyConnected, ToContract(existing));
        }

        var info = await github.GetInstallationAsync(installationId, cancellationToken);
        if (info is null)
        {
            return (ConnectGitHubOutcome.NotAccessible, null);
        }

        var now = time.GetUtcNow();
        var installation = new GitHubInstallation
        {
            Id = Guid.CreateVersion7(now),
            OrgId = orgId,
            InstallationId = installationId,
            AccountLogin = Truncate(info.AccountLogin, GitHubInstallation.AccountLoginMaxLength),
            AccountType = Truncate(info.AccountType, GitHubInstallation.AccountTypeMaxLength),
            ConnectedBy = actorId,
            ConnectedAt = now,
        };
        db.Installations.Add(installation);
        db.AddActivityEvent(InstallationEvent(GitHubEventTypes.GitHubConnected, installation, actorId, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: GitHubDbContext.InstallationIdIndexName,
            })
        {
            // RLS hid the row: it belongs to another organization (or this one connected it a moment ago).
            db.ChangeTracker.Clear();
            var mine = await db.Installations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.InstallationId == installationId, cancellationToken);
            return mine is null
                ? (ConnectGitHubOutcome.ConnectedElsewhere, null)
                : (ConnectGitHubOutcome.AlreadyConnected, ToContract(mine));
        }

        return (ConnectGitHubOutcome.Connected, ToContract(installation));
    }

    public async Task<bool> DisconnectAsync(Guid connectionId, Guid actorId, CancellationToken cancellationToken)
    {
        var installation = await db.Installations.FirstOrDefaultAsync(i => i.Id == connectionId, cancellationToken);
        if (installation is null)
        {
            return false;
        }

        var now = time.GetUtcNow();
        var links = await db.ProjectRepositories
            .Where(l => l.InstallationRowId == installation.Id)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            var project = await projects.FindAsync(link.ProjectId, cancellationToken);
            db.AddActivityEvent(ProjectRepositoryEvents.Unlinked(
                link, project?.Key, installation.OrgId, actorId, now, ProjectRepositoryEvents.InstallationDisconnectedReason));
            db.ProjectRepositories.Remove(link);
        }

        db.Installations.Remove(installation);
        db.AddActivityEvent(InstallationEvent(GitHubEventTypes.GitHubDisconnected, installation, actorId, now));
        await db.SaveChangesAsync(cancellationToken);

        github.ForgetInstallation(installation.InstallationId);
        return true;
    }

    public async Task<(ListRepositoriesOutcome Outcome, GitHubRepositoryList? Repositories)> ListRepositoriesAsync(
        Guid connectionId, CancellationToken cancellationToken)
    {
        if (!app.IsConfigured)
        {
            return (ListRepositoriesOutcome.NotConfigured, null);
        }

        var installation = await db.Installations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == connectionId, cancellationToken);
        if (installation is null)
        {
            return (ListRepositoriesOutcome.ConnectionNotFound, null);
        }

        try
        {
            var (items, truncated) = await github.ListRepositoriesAsync(installation.InstallationId, MaxRepositories, cancellationToken);
            return (ListRepositoriesOutcome.Listed, new GitHubRepositoryList(
                items.Select(r => new GitHubRepository(r.Id, r.FullName, r.DefaultBranch, r.Private, r.HtmlUrl)).ToList(),
                truncated));
        }
        catch (GitHubInstallationGoneException)
        {
            return (ListRepositoriesOutcome.InstallationGone, null);
        }
    }

    private static ActivityEnvelope InstallationEvent(
        string eventType, GitHubInstallation installation, Guid actorId, DateTimeOffset now)
        => ActivityEnvelope.Create(
            eventType,
            now,
            installation.OrgId,
            EventActor.User(actorId),
            // No key: an account login can be longer than the activity log's entity keys.
            new EventEntity(GitHubInstallation.EntityType, installation.Id, Key: null),
            projectId: null,
            new JsonObject
            {
                ["installationId"] = installation.InstallationId,
                ["account"] = installation.AccountLogin,
                ["accountType"] = installation.AccountType,
            });

    private static GitHubConnection ToContract(GitHubInstallation installation)
        => new(
            installation.Id,
            installation.InstallationId,
            installation.AccountLogin,
            installation.AccountType,
            GitHubUrls.Installation(installation.InstallationId, installation.AccountLogin, installation.AccountType),
            installation.ConnectedAt);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
