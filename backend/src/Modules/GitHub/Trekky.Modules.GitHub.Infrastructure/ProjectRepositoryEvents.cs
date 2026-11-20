using System.Text.Json.Nodes;
using Trekky.Modules.GitHub.Domain;
using Trekky.SharedKernel.Events;

namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>The activity events about a project's repository link; their entity is the project, so they show in its feed.</summary>
internal static class ProjectRepositoryEvents
{
    public const string ProjectEntityType = "project";

    public const string UnlinkedReason = "unlinked";

    public const string InstallationDisconnectedReason = "installationDisconnected";

    public static ActivityEnvelope Linked(ProjectRepository link, string? projectKey, Guid actorId, DateTimeOffset now)
        => Create(GitHubEventTypes.ProjectRepositoryLinked, link, projectKey, link.OrgId, actorId, now, Settings(link));

    public static ActivityEnvelope Changed(
        ProjectRepository link, JsonObject previous, string? projectKey, Guid actorId, DateTimeOffset now)
    {
        var payload = Settings(link);
        payload["previous"] = previous;
        return Create(GitHubEventTypes.ProjectRepositoryChanged, link, projectKey, link.OrgId, actorId, now, payload);
    }

    public static ActivityEnvelope Unlinked(
        ProjectRepository link, string? projectKey, Guid orgId, Guid actorId, DateTimeOffset now, string reason)
    {
        var payload = Settings(link);
        payload["reason"] = reason;
        return Create(GitHubEventTypes.ProjectRepositoryUnlinked, link, projectKey, orgId, actorId, now, payload);
    }

    /// <summary>The settings as events carry them: <c>{ "repository", "docsPath", "branch" }</c>.</summary>
    public static JsonObject Settings(ProjectRepository link)
        => new()
        {
            ["repository"] = link.RepositoryFullName,
            ["docsPath"] = link.DocsPath,
            ["branch"] = link.Branch,
        };

    private static ActivityEnvelope Create(
        string eventType, ProjectRepository link, string? projectKey, Guid orgId, Guid actorId, DateTimeOffset now, JsonObject payload)
        => ActivityEnvelope.Create(
            eventType,
            now,
            orgId,
            EventActor.User(actorId),
            new EventEntity(ProjectEntityType, link.ProjectId, projectKey),
            link.ProjectId,
            payload);
}
