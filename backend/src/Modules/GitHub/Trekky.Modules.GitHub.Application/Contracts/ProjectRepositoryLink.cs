namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>Where a project's docs live on GitHub (TREK-74).</summary>
/// <param name="InstallationId">Trekky's id of the connection (<see cref="GitHubConnection.Id"/>) that reaches the repository.</param>
/// <param name="Account">The account of that connection.</param>
/// <param name="RepositoryId">GitHub's repository id.</param>
/// <param name="Repository"><c>owner/name</c>.</param>
/// <param name="DocsPath">The docs folder with a trailing <c>/</c> (<c>docs/</c>), or empty for the repository root.</param>
/// <param name="Branch">The branch docs are read from and pull requests target.</param>
/// <param name="HtmlUrl">The docs folder on GitHub.</param>
public sealed record ProjectRepositoryLink(
    Guid ProjectId,
    Guid InstallationId,
    string Account,
    long RepositoryId,
    string Repository,
    string DocsPath,
    string Branch,
    string HtmlUrl,
    DateTimeOffset LinkedAt);
