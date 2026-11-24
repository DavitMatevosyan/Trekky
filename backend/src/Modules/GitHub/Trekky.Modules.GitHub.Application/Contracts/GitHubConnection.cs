namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A connected installation of the Trekky GitHub App.</summary>
/// <param name="Id">Trekky's id for the connection.</param>
/// <param name="InstallationId">GitHub's installation id.</param>
/// <param name="Account">The GitHub user or organization it is installed on.</param>
/// <param name="AccountType"><c>User</c> or <c>Organization</c>.</param>
/// <param name="ManageUrl">The installation's page on GitHub, where repositories are chosen and the app is uninstalled.</param>
public sealed record GitHubConnection(
    Guid Id, long InstallationId, string Account, string AccountType, string ManageUrl, DateTimeOffset ConnectedAt);
