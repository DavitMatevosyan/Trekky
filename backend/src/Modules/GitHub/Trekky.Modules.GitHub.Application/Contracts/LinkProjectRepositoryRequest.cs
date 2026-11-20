namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>Body of <c>PUT /api/v1/projects/{projectId}/github</c>.</summary>
/// <param name="InstallationId">The connection (<see cref="GitHubConnection.Id"/>) that reaches the repository.</param>
/// <param name="Repository"><c>owner/name</c>.</param>
/// <param name="DocsPath">The docs folder; <c>docs/</c> when omitted, <c>""</c> or <c>/</c> for the repository root.</param>
/// <param name="Branch">The branch; <c>dev</c> when omitted.</param>
public sealed record LinkProjectRepositoryRequest(Guid? InstallationId, string? Repository, string? DocsPath, string? Branch);
