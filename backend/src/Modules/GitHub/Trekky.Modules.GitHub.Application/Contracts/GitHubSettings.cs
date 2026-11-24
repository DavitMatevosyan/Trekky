namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>The organization's GitHub connection, for its settings page (TREK-74).</summary>
/// <param name="Configured">Whether this server has a GitHub App set up; without one nothing can be connected.</param>
/// <param name="InstallUrl">Where an Admin installs the app (add <c>?state=</c>); <c>null</c> when not configured.</param>
/// <param name="Installations">The connected installations, oldest first.</param>
public sealed record GitHubSettings(bool Configured, string? InstallUrl, IReadOnlyList<GitHubConnection> Installations);
