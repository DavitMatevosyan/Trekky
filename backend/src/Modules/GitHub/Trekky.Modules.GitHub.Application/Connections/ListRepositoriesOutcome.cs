namespace Trekky.Modules.GitHub.Application.Connections;

internal enum ListRepositoriesOutcome
{
    Listed,
    NotConfigured,

    /// <summary>The organization has no such connection.</summary>
    ConnectionNotFound,

    /// <summary>The app was uninstalled on GitHub; the connection should be removed.</summary>
    InstallationGone,
}
