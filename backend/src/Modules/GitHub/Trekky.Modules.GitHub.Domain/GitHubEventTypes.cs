namespace Trekky.Modules.GitHub.Domain;

/// <summary>Activity events the GitHub module emits (docs/tech/activity-events.md).</summary>
internal static class GitHubEventTypes
{
    /// <summary>Entity: the installation. Payload: <c>{ "installationId": 123, "account": "acme", "accountType": "Organization" }</c>.</summary>
    public const string GitHubConnected = "GitHubConnected";

    /// <summary>Entity: the installation. Payload: as <see cref="GitHubConnected"/>.</summary>
    public const string GitHubDisconnected = "GitHubDisconnected";

    /// <summary>Entity: the project. Payload: <c>{ "repository": "acme/handbook", "docsPath": "docs/", "branch": "dev" }</c>.</summary>
    public const string ProjectRepositoryLinked = "ProjectRepositoryLinked";

    /// <summary>Entity: the project. Payload: the new settings plus <c>"previous"</c> with the old ones.</summary>
    public const string ProjectRepositoryChanged = "ProjectRepositoryChanged";

    /// <summary>
    /// Entity: the project. Payload: the settings that were removed plus <c>"reason"</c>: <c>"unlinked"</c>, or
    /// <c>"installationDisconnected"</c> when its installation was disconnected.
    /// </summary>
    public const string ProjectRepositoryUnlinked = "ProjectRepositoryUnlinked";
}
