namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>GitHub no longer knows the installation: the app was uninstalled or suspended there.</summary>
internal sealed class GitHubInstallationGoneException : Exception
{
    public GitHubInstallationGoneException()
    {
    }

    public GitHubInstallationGoneException(string message)
        : base(message)
    {
    }

    public GitHubInstallationGoneException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
