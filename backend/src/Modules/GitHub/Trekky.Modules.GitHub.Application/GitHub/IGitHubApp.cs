namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>Whether this server has a GitHub App set up (configuration <c>GitHub:*</c>), and where to install it.</summary>
internal interface IGitHubApp
{
    bool IsConfigured { get; }

    /// <summary><c>https://github.com/apps/{slug}/installations/new</c>, or <c>null</c> when not configured.</summary>
    string? InstallUrl { get; }
}
