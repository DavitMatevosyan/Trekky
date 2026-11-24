namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>
/// Body of <c>POST /api/v1/github/installations</c>: what GitHub's redirect brought back after the install. The code
/// proves which GitHub user installed the app; the installation is only connected if that user can access it.
/// </summary>
public sealed record ConnectGitHubRequest(string? Code, long? InstallationId);
