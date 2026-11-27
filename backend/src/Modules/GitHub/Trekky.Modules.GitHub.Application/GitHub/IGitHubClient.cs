namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>
/// GitHub's REST API as this module uses it (ADR-0028). The implementation signs in as the app (a short-lived JWT), as
/// an installation (a token it caches in memory until shortly before it expires) or, once, as the user who installed
/// the app. No token leaves the server. Calls throw <see cref="GitHubUnavailableException"/> when GitHub cannot answer.
/// </summary>
internal interface IGitHubClient
{
    /// <summary>Exchanges the code from the install redirect for a one-off user token; <c>null</c> when GitHub rejects the code.</summary>
    Task<string?> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>The ids of this app's installations the user behind <paramref name="userToken"/> can access.</summary>
    Task<IReadOnlySet<long>> ListUserInstallationIdsAsync(string userToken, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the user behind <paramref name="userToken"/> administers every repository the installation reaches, the
    /// right they would need to install the app there themselves. Reading one repository is not enough to connect it.
    /// </summary>
    Task<bool> UserAdministersInstallationAsync(string userToken, long installationId, CancellationToken cancellationToken);

    /// <summary>Ends the one-off user token on GitHub; best effort, never throws for GitHub's answer.</summary>
    Task RevokeUserTokenAsync(string userToken, CancellationToken cancellationToken);

    /// <summary>The installation as the app sees it; <c>null</c> when it does not exist (any more).</summary>
    Task<GitHubInstallationInfo?> GetInstallationAsync(long installationId, CancellationToken cancellationToken);

    /// <summary>
    /// The repositories the installation can reach, by name, up to <paramref name="max"/>. Throws
    /// <see cref="GitHubInstallationGoneException"/> when the app was uninstalled.
    /// </summary>
    Task<(IReadOnlyList<GitHubRepositoryInfo> Items, bool Truncated)> ListRepositoriesAsync(
        long installationId, int max, CancellationToken cancellationToken);

    Task<bool> BranchExistsAsync(long installationId, string fullName, string branch, CancellationToken cancellationToken);

    /// <summary>What is at <paramref name="path"/> (a folder path without trailing <c>/</c>; empty for the root) on the branch.</summary>
    Task<GitHubPathKind> GetPathKindAsync(
        long installationId, string fullName, string branch, string path, CancellationToken cancellationToken);

    /// <summary>The commit at the tip of <paramref name="branch"/>; <c>null</c> when the branch does not exist.</summary>
    Task<GitHubCommitInfo?> GetBranchHeadAsync(
        long installationId, string fullName, string branch, CancellationToken cancellationToken);

    /// <summary>
    /// The SHA of the folder at <paramref name="path"/> (no trailing <c>/</c>; not empty) in commit
    /// <paramref name="commitSha"/>; <c>null</c> when there is no folder there.
    /// </summary>
    Task<string?> GetFolderShaAsync(
        long installationId, string fullName, string commitSha, string path, CancellationToken cancellationToken);

    /// <summary>Every entry under the tree <paramref name="treeSha"/>, recursively.</summary>
    Task<GitHubTree> GetTreeAsync(long installationId, string fullName, string treeSha, CancellationToken cancellationToken);

    /// <summary>The bytes of the blob <paramref name="sha"/>.</summary>
    Task<byte[]> GetBlobAsync(long installationId, string fullName, string sha, CancellationToken cancellationToken);

    /// <summary>The last commit on <paramref name="branch"/> that changed <paramref name="path"/>; <c>null</c> when none did.</summary>
    Task<GitHubCommitInfo?> GetLastCommitAsync(
        long installationId, string fullName, string branch, string path, CancellationToken cancellationToken);

    /// <summary>
    /// The bytes of the file at <paramref name="path"/> in commit <paramref name="commitSha"/>, up to
    /// <paramref name="maxBytes"/>; <c>null</c> when there is no file there or it is larger.
    /// </summary>
    Task<byte[]?> GetFileAsync(
        long installationId, string fullName, string commitSha, string path, int maxBytes, CancellationToken cancellationToken);

    /// <summary>Drops the installation's cached token, so a disconnected installation keeps nothing in memory.</summary>
    void ForgetInstallation(long installationId);
}
