using Trekky.Modules.GitHub.Application.Contracts;

namespace Trekky.Modules.GitHub.Application.Connections;

/// <summary>The current organization's connected GitHub App installations (TREK-74).</summary>
internal interface IGitHubConnectionService
{
    Task<GitHubSettings> GetSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Connects the installation if the user behind the code administers every repository it reaches on GitHub, with its
    /// <c>GitHubConnected</c> event in one transaction. Connecting one the organization already has returns it.
    /// Input must be validated.
    /// </summary>
    Task<(ConnectGitHubOutcome Outcome, GitHubConnection? Connection)> ConnectAsync(
        ConnectGitHubRequest request, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the installation, unlinks the projects that used it and drops its cached token. The app stays installed
    /// on GitHub until someone uninstalls it there. <c>false</c> when the organization has no such connection.
    /// </summary>
    Task<bool> DisconnectAsync(Guid connectionId, Guid actorId, CancellationToken cancellationToken);

    Task<(ListRepositoriesOutcome Outcome, GitHubRepositoryList? Repositories)> ListRepositoriesAsync(
        Guid connectionId, CancellationToken cancellationToken);
}
