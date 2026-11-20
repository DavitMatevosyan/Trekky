namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>An installation of the app as GitHub describes it.</summary>
/// <param name="AccountType"><c>User</c> or <c>Organization</c>.</param>
internal sealed record GitHubInstallationInfo(long Id, string AccountLogin, string AccountType);
