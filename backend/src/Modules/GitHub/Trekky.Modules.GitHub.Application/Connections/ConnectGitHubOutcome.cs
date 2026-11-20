namespace Trekky.Modules.GitHub.Application.Connections;

internal enum ConnectGitHubOutcome
{
    Connected,

    /// <summary>The organization had already connected this installation; nothing changed.</summary>
    AlreadyConnected,

    /// <summary>This server has no GitHub App set up.</summary>
    NotConfigured,

    /// <summary>GitHub rejected the code (expired, used or made up).</summary>
    CodeRejected,

    /// <summary>The GitHub user behind the code cannot access the installation, does not administer all of it, or it does not exist.</summary>
    NotAccessible,

    /// <summary>Another Trekky organization already connected the installation.</summary>
    ConnectedElsewhere,
}
