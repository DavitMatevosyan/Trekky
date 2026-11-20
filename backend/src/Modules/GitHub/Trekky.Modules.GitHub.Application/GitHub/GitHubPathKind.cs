namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>What a path in a repository is on a given branch.</summary>
internal enum GitHubPathKind
{
    Missing,
    Folder,
    File,
}
