namespace Trekky.Modules.GitHub.Application.Repositories;

internal enum LinkRepositoryOutcome
{
    Created,
    Updated,
    NotConfigured,
    ProjectNotFound,

    /// <summary>The organization has no such connection.</summary>
    ConnectionNotFound,

    /// <summary>The app was uninstalled on GitHub.</summary>
    InstallationGone,

    /// <summary>The repository does not exist or the installation was not given access to it.</summary>
    RepositoryNotFound,

    BranchNotFound,

    /// <summary>The docs folder does not exist on the branch, or is a file.</summary>
    DocsFolderNotFound,

    /// <summary>The project already has a link and <c>If-Match</c> was not sent.</summary>
    IfMatchRequired,

    /// <summary><c>If-Match</c> does not match the current link (or there is none); the result carries the latest.</summary>
    Stale,
}
