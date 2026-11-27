namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>How a docs request ended (TREK-75); each maps to one HTTP answer.</summary>
internal enum DocsOutcome
{
    Ok,
    NotConfigured,
    NotLinked,
    InstallationGone,

    /// <summary>GitHub could not be reached and nothing is cached yet.</summary>
    Unavailable,
    InvalidPath,
    NotFound,
}
